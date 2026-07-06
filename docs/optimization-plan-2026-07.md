# DataTables 二期优化评审与新计划

> 评审日期：2026-07-06  
> 范围：`src/DataTables`、`src/DataTables.GeneratorCore`、生成模板、测试目录与当前落地的架构改动。

## 1. 当前落地进展复盘

上一轮规划中的多项基础任务已经进入代码实现阶段，整体方向正确，项目已经从“单体处理器 + 固定运行时加载”逐步过渡到“可分层、可注册、可诊断、可版本化”的结构。

### 1.1 已完成或基本完成的能力

| 方向 | 当前状态 | 代表实现 |
| --- | --- | --- |
| Schema 解析分层 | 已初步落地 | `Schema/TableSchemaService`、`Schema/*TableParser`、`ITableSchemaParserRegistry` |
| 表类型注册表 | 已初步落地 | `TableSchemaParserRegistry` 支持 `table` / `matrix` / `column`，并记录预留类型 |
| 类型系统解析 | 已初步落地 | `DataTypeParser`、`DataTypeDescriptor`、`DataTypeParseException` |
| 运行时预热 | 已初步落地 | `TableRegistration`、`PreheatAsync`、生成的 `TableRegistrations` |
| 运行时统计 | 已初步落地 | `LoadStats` 增加成功数、失败数和累计统计字段 |
| 数据源管线 | 已初步落地 | `CachedDataSource`、`CompressedDataSource`、`EncryptedDataSource`、`FallbackDataSource`、`VersionedDataSource` |
| 二进制结构化头 | 已初步落地 | `DataTableBinaryWriter` 写入 schema hash、generator version、table full name、flags |
| Schema Hash | 已初步落地 | `DataTableSchemaHash` 与 `DataTableBase.SchemaHash` |
| 索引元数据 | 已初步落地 | `IndexDefinition`、`GroupIndexDefinition`、`UniqueConstraint` |
| 索引 API 生成 | 已初步落地 | `GetBy`、`TryGetBy`、`Contains`、`GetManyBy` |
| 取消语义 | 已初步落地 | 调用方取消只取消等待，不取消共享加载 |

## 2. 重新评审后的关键发现

### 2.1 架构分层已经启动，但旧逻辑仍未完全收敛

`DataTableProcessor` 已经把 `CreateGenerationContext` 委托给 `TableSchemaService`，校验也委托给 `TableSchemaValidator`，这是正确方向。但文件中仍保留了部分旧解析辅助函数、二进制写出常量和数据写出相关逻辑，短期可兼容，长期会让职责边界再次模糊。

**风险：** 后续新增 `kv`、`localized`、`partitioned` 等表类型时，容易再次把逻辑塞回 `DataTableProcessor`。

**新计划：** 二期应继续把 `DataTableProcessor` 降级为门面与编排类，把 Schema、Validation、Serialization、CodeGen、Diagnostics 完整拆开。

### 2.2 二进制格式已升级到 v3，但兼容策略需要补齐

当前生成端与运行时都使用格式版本 3，并加入了结构化 header 与 schema hash 校验。这能有效发现“代码与数据不同步”的问题，但当前运行时直接拒绝非 v3 数据。

**风险：** 存量项目如果仍有 v2 `.bytes`，升级运行时后会直接失败；Unity 侧如果旧包和新数据混用，也会产生较硬的迁移成本。

**新计划：** 增加 v2 兼容读取路径或明确提供迁移工具，并在文档中说明 v2 到 v3 的升级要求。

### 2.3 预热实现方向正确，但仍依赖运行时反射扫描扩展类

`DataTableManager` 通过扫描已加载程序集查找名为 `DataTableManagerExtension` 的类型，再读取 `TableRegistrations`。这避免了用户显式注册，但仍存在运行时反射成本和命名冲突风险。

**风险：** 多程序集、多命名空间、多份生成代码共存时，可能拿到错误的扩展类或只拿到第一份扩展类。

**新计划：** 引入显式注册入口，例如 `DataTableManager.RegisterTables(IReadOnlyList<TableRegistration>)`，生成代码在模块初始化或用户调用初始化时注册，反射扫描只作为兼容兜底。

### 2.4 数据源管线已出现，但压缩/加密 flags 与装饰器未形成闭环

运行时已具备 `CompressedDataSource`、`EncryptedDataSource` 等装饰器，二进制 header 也有 flags，但当前 `ValidateStructuredHeader` 对非零 flags 直接报错。

**风险：** 生成端一旦开始写入压缩/加密标记，运行时虽然有装饰器但不能通过 header 校验；如果压缩/加密在数据源外层完成，header flags 又缺乏统一约定。

**新计划：** 明确两层模型：

1. **传输层处理**：由 `IDataSource` 装饰器解压/解密，进入 `DataTableManager` 的一定是明文 DataTables payload，此时 flags 可为 0。
2. **Payload 层处理**：DataTables payload 自身标记压缩/加密，`DataTableManager` 根据 flags 选择解码器。

二期建议先采用传输层模型，flags 仅保留扩展位，并在文档中明确。

### 2.5 类型解析器增强了诊断，但类型处理器缓存仍存在状态污染风险

`DataProcessorUtility.GetDataProcessor` 会把 `descriptor` 赋给 `dataProcessor.TypeDescriptor`。基础类型处理器是缓存共享实例，如果后续逻辑依赖 `TypeDescriptor`，并发生成或嵌套类型解析时可能存在状态覆盖风险。

**风险：** 多线程并发生成时，共享 `DataProcessor` 实例带可变状态，会产生隐性 bug。

**新计划：** 让 `DataProcessor` 尽量无状态，`DataTypeDescriptor` 作为方法参数传递；或让每次 `GetDataProcessor` 返回不可变处理器实例，避免共享实例写入可变字段。

### 2.6 索引生成已经加强，但唯一性运行时校验仍依赖 Dictionary.Add 异常

生成模板使用 `m_Dict.Add(...)` 添加唯一索引，重复时会抛出异常，但错误上下文不够友好，不能直接指出重复字段值来自哪一行。

**风险：** 策划排查重复索引时定位成本高。

**新计划：** 在生成期增加可选唯一性预校验，或在运行时生成更明确的异常信息，至少包含索引名、字段列表、重复 key 和表名。

### 2.7 文档需要跟上新架构，否则能力不可发现

当前代码已经实现多项新能力，但 README 和细分文档未必完整覆盖：v3 二进制格式、schema hash、数据源管线、注册式解析器、索引 API、取消语义等都需要稳定文档。

**风险：** 功能实现了但用户不会用，或者继续按旧方式接入导致踩坑。

**新计划：** 二期必须把文档作为交付项，而不是最后补充。

## 3. 二期优化目标

二期目标不是继续堆功能，而是把已经落地的架构能力打磨成稳定、可维护、可扩展的产品化基础。

### 3.1 总目标

1. **稳定 v3 格式与迁移路径**：明确 v2 兼容策略、schema hash 校验、数据与代码同步机制。
2. **完成生成器职责分层**：让 `DataTableProcessor` 只负责编排。
3. **预热注册去反射化**：支持显式表注册，兼容旧反射兜底。
4. **数据源管线闭环**：明确传输层解码模型，完善 manifest 与版本校验。
5. **类型系统无状态化**：消除共享处理器可变状态风险。
6. **索引诊断产品化**：让索引错误可定位、可理解、可修复。
7. **文档与测试同步补齐**：每个核心能力都有文档和测试覆盖。

## 4. 新实施计划

### 阶段 A：稳定性修复与架构收口（优先级 P0）

#### A1. 收口 `DataTableProcessor` 职责

- 将仍留在 `DataTableProcessor` 内的旧解析辅助函数迁移或删除。
- 将数据写出入口统一路由到 `IDataTableBinaryWriter`。
- 保留 `DataTableProcessor` 作为兼容门面，外部调用不破坏。

**验收标准：**

- `DataTableProcessor` 不直接承担 Schema 解析细节。
- 二进制 header 常量只保留一份权威定义。
- 既有测试通过。

#### A2. 修复类型处理器共享状态风险

- 移除或限制 `DataProcessor.TypeDescriptor` 的可变写入。
- 将类型描述作为解析结果传递给构造器或方法参数。
- 确保 `array<map<string,int>>` 等嵌套类型在并发生成下无共享状态污染。

**验收标准：**

- 新增并发类型解析测试。
- 新增嵌套复合类型测试。
- `DataProcessor` 基础类型处理器保持无状态或线程安全。

#### A3. 明确 v2/v3 二进制迁移策略

二选一：

1. **兼容读取 v2**：运行时根据版本走旧 header 读取路径。
2. **强制迁移 v3**：提供生成器命令或文档，要求代码和 bytes 同步再生成。

建议采用：短期强制迁移 v3 + 明确错误信息；中期增加 v2 只读兼容以降低项目升级成本。

**验收标准：**

- 文档明确 v3 header 字段。
- 错误提示能指导用户重新生成代码和数据。
- 若实现 v2 兼容，应有 v2 fixture 测试。

### 阶段 B：运行时注册、预热与数据源闭环（优先级 P0/P1）

#### B1. 增加显式表注册 API

新增建议 API：

```csharp
DataTableManager.RegisterTables(DataTableManagerExtension.TableRegistrations);
DataTableManager.ClearTableRegistrations();
```

生成代码可继续暴露 `TableRegistrations`，并提供：

```csharp
DataTableManagerExtension.Register();
```

**验收标准：**

- `PreheatAsync` 优先使用显式注册表。
- 反射扫描仅作为兼容兜底。
- 多程序集注册时可以合并，而不是只取第一份扩展类。

#### B2. 预热统计细化

当前 `PreheatAsync` 以成功加载数量估算失败数量。二期应区分：

- 已在缓存中命中。
- 本次真实加载成功。
- 加载失败。
- 取消。
- 未注册任何表。

**验收标准：**

- `LoadStats` 或新增 `PreheatStats` 能表达上述状态。
- profiling hook 收到的数据可用于性能面板。
- 取消预热不会污染失败统计。

#### B3. 数据源 manifest 与版本校验

- 为 `DataSourceManifestEntry` 的 hash/version 制定格式。
- `VersionedDataSource` 应能过滤或映射 manifest entry，而不只是简单附加 version。
- `FallbackDataSource` 应记录命中来源，方便诊断。

**验收标准：**

- 可以通过 manifest 判断本地数据是否过期。
- 失败日志能说明从哪个数据源加载失败、最终命中哪个数据源。

### 阶段 C：索引、表类型与校验增强（优先级 P1）

#### C1. 生成期唯一索引预校验

- 在数据写出前扫描索引字段组合。
- 重复时报告文件、Sheet、索引字段、重复值、行号。
- 支持开关：严格模式报错，兼容模式仅警告。

**验收标准：**

- 策划可以直接根据错误定位 Excel 行。
- 运行时不再是唯一发现重复索引的地方。

#### C2. 新增 `kv` 表类型原型

`kv` 用于全局参数和开关配置，推荐格式：

| Key | Type | Value | Comment |
| --- | --- | --- | --- |
| MaxLevel | int | 100 | 最大等级 |
| EnablePvp | bool | true | 是否开启 PVP |

生成代码可以是强类型属性：

```csharp
DTGameConfig.MaxLevel
DTGameConfig.EnablePvp
```

**验收标准：**

- `DTGen=kv` 被注册表识别。
- 支持基础类型、enum、array、json。
- 文档给出 Excel 示例。

#### C3. 新增 `localized` 表类型设计文档

暂不急于实现，先完成设计：

- key + 多语言列。
- 语言 fallback。
- 客户端按当前语言加载或全量加载。
- 服务端可按语言导出。

**验收标准：**

- 设计文档明确格式、生成代码形态、运行时查询 API。

### 阶段 D：文档、测试与开发体验（优先级 P1/P2）

#### D1. 文档拆分

建议新增：

```text
docs/
  architecture.md
  binary-format-v3.md
  generator-pipeline.md
  runtime-loading.md
  data-source-pipeline.md
  table-types.md
  type-system.md
  indexing.md
  migration-v2-to-v3.md
```

#### D2. 测试矩阵补齐

重点测试：

- v3 header 校验。
- schema hash 不匹配。
- 显式表注册预热。
- 多程序集/多 namespace 的注册行为。
- 数据源 fallback、cache、versioned。
- 类型解析并发与嵌套泛型。
- 索引重复诊断。
- `kv` 表解析与生成。

#### D3. 示例与迁移指南

至少补充三类示例：

1. Unity 本地包 + 后台预热。
2. 服务端全量预热 + 显式注册。
3. CDN 远程数据源 + 本地缓存回退。

## 5. 建议里程碑

### Milestone 1：二期稳定版

- 收口 `DataTableProcessor`。
- 修复类型处理器状态风险。
- 明确 v3 迁移文档。
- 显式表注册 API。
- 预热统计修正。

### Milestone 2：生产运行时版

- 数据源 manifest 与版本校验。
- fallback 诊断增强。
- profiling 统计可用于面板。
- Unity / 服务端示例补齐。

### Milestone 3：配置表类型扩展版

- `kv` 表类型实现。
- `localized` 设计文档。
- 索引唯一性生成期校验。
- 表类型扩展指南。

### Milestone 4：质量与生态版

- 完整 docs 目录。
- Benchmark。
- 迁移工具或 v2 兼容读取。
- CI 测试矩阵。

## 6. 当前优先级排序

| 优先级 | 任务 | 原因 |
| --- | --- | --- |
| P0 | 显式表注册 API | 解决预热依赖反射扫描和多程序集不确定性 |
| P0 | 类型处理器无状态化 | 避免并发生成隐性 bug |
| P0 | v3 迁移策略文档 | 避免用户升级后 bytes 不兼容 |
| P1 | DataTableProcessor 职责收口 | 保持架构分层不倒退 |
| P1 | 预热统计细化 | 提升运行时可观测性 |
| P1 | 数据源 manifest 版本校验 | 支撑热更新和 CDN 场景 |
| P1 | 索引重复诊断 | 降低策划排错成本 |
| P2 | `kv` 表类型 | 扩大配置表类型覆盖 |
| P2 | `localized` 设计 | 为多语言配置打基础 |
| P2 | 示例与文档矩阵 | 提升可用性和接入效率 |

## 7. 二期完成定义

二期完成时，项目应达到以下状态：

1. 生成器分层清晰，新增表类型不需要改动核心门面类。
2. 运行时预热不依赖不确定的全程序集扫描。
3. v3 二进制格式有清晰文档、迁移策略和测试。
4. 类型系统在并发生成下稳定。
5. 数据源管线有明确使用模型和版本校验路径。
6. 索引错误能在生成期给出可定位诊断。
7. 至少新增一个配置表类型原型，例如 `kv`。
8. 文档、测试、示例与代码能力保持同步。
