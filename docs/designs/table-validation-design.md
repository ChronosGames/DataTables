# 表类型数据校验器设计

> 状态：proposed（2026-07-28 深度评审修订）<br>
> 初稿日期：2026-07-27<br>
> 范围：生成期/`validate` 命令的数据合理性与语义校验；不改变 v3 `.bytes` 运行时协议。

## 1. 问题与目标

DataTables 已能校验字段、索引等 Schema 约束，也会在序列化时发现单元格格式错误；`tree` 和 `graph` 还在 `DataRowBinarySerializer` 中执行部分结构校验。但是这些能力分散在 parser、通用 Schema validator 和 writer 中，新增表类型时没有统一的校验扩展点。

本设计希望形成一条可注册、可组合、可定位的生成期校验管线，使每种表既能复用基础规则，也能声明自己的业务语义：

- **合理性**：必填、范围、长度、枚举集合、唯一性、顺序等单表规则；
- **语义准确性**：外键存在、树无环、图约束、矩阵坐标和值域等结构或跨表规则；
- **可操作诊断**：每个问题尽量包含文件、Sheet、逻辑表、字段、单元格和规则码；
- **生成安全**：存在 Error 时不产出代码和 `.bytes`，Warning 可由严格模式升级；
- **可扩展性**：新增表类型通过注册 validator 接入，不修改 `DataTableProcessor` 主流程。

非目标：

- 在运行时加载每张表时重复执行完整业务校验；
- 在第一阶段实现任意 C# 表达式或脚本执行；
- 为旧版 `.bytes` 增加兼容分支；
- 猜测业务含义，例如看到字段名 `Price` 就自动要求其大于零。

## 2. 当前能力与缺口

当前流程大致为：

```text
ITableSchemaParser
  -> TableSchemaValidator
  -> DataRowBinarySerializer
  -> DataTableBinaryWriter
```

主要缺口如下：

1. `ITableSchemaValidator` 只接收 `firstDataRowIndex`，无法统一访问工作表数据、源单元格位置或其他逻辑表。
2. `TableSchemaValidator` 同时承担通用前置检查和索引规范化，类型专属规则没有注册机制。
3. `tree` / `graph` 的引用、环和数字规则位于 serializer；当前 `ValidateOnly` 不调用 `GenerateDataFile`，所以这些规则不会执行，而正常生成又要等到写出阶段才发现错误。
4. 多数失败通过 `Exception` / `FormatException` 表达，只能报告第一个问题，策划需要反复修表、重跑。
5. `table`、`matrix`、`column` 只有单元格能否被类型处理器解析的校验，没有声明式值域、唯一性或引用完整性规则。
6. 跨 Sheet/跨文件语义必须在所有目标表完成解析之后执行，现有逐 Sheet 校验时机不足。

### 2.1 深度评审结论：初稿仍不足以直接实施

初稿正确识别了“按类型注册 validator”和“单表/项目两阶段校验”的方向，但接口草图和实施顺序仍有以下关键不足。若直接照初稿编码，容易新增另一套与 parser、serializer 并行且逐渐漂移的数据模型。

#### 不足一：`ValidationRow` 复制数据，却没有定义唯一解析来源

初稿同时保留 `RawText`、`object? ParsedValue`，并提出 serializer 共享解析结果，但没有回答由谁解析、失败值如何表达、复合类型如何避免可变对象泄漏。`object` 还会丢失 `DataTypeDescriptor`、空值状态和原始 Excel 类型，validator 最终仍需类型判断或再次转换。

**修正建议：** 不把初稿代码片段视为冻结 API。先引入不可变的 `TableDataSnapshot`，其中每个值由以下信息组成：

- 字段稳定 ID 与 `DataTypeDescriptor`；
- 原始文本、源 Excel 类型和 `SourceSpan`；
- `Missing` / `Empty` / `Parsed` / `Invalid` 四态解析结果；
- 成功时的只读规范值，失败时的结构化转换诊断。

类型转换只能由 reader/materializer 调用一次。validator 和 writer 消费同一个 snapshot，不允许再调用 `DataProcessor.WriteToStream` 来“顺便验证”。在该模型落地前，不应先公开 `ValidationRow` API。

#### 不足二：只增加 validator registry 会制造多个不一致的注册表

当前已经存在 parser 注册表，模板选择和 serializer 分支却在其他位置。再单独增加 validator registry，只能靠测试事后发现 parser、validator、writer、template 支持集合漂移；当前 reserved 与 supported 名称也可能出现重叠，说明分散注册已经有维护风险。

**修正建议：** 以单一 `TableTypeDescriptor` 作为组合根：

```csharp
internal sealed class TableTypeDescriptor
{
    public string DataSetType { get; }
    public ITableSchemaParser Parser { get; }
    public ITableDataMaterializer Materializer { get; }
    public IReadOnlyList<ITableValidationPass> ValidationPasses { get; }
    public ITableSerializationPlan Serialization { get; }
    public ITableCodeRenderer CodeRenderer { get; }
}
```

默认 registry 一次性注册完整 descriptor，并在启动时验证类型名唯一、组件声明一致、reserved 与 supported 不相交。若暂时无法统一模板和 writer，过渡期 registry 也必须成为唯一支持类型清单，其他注册点由它派生，而不是各自维护字符串数组。

#### 不足三：validator 生命周期、顺序和失败级联没有契约

初稿给出了大致顺序，却未定义前序解析失败后哪些规则仍可运行。例如 Id 无法解析时继续做唯一性和外键检查会产生大量派生错误；项目 validator 在目标表本身失败时也无法判断引用是否真的缺失。

**修正建议：** 每个 pass 明确 `Phase`、`Requires`、`Produces` 和 `CanRunOnInvalidInput`。建议阶段固定为：

```text
Schema -> Materialization -> LocalStructural -> LocalSemantic
       -> ProjectReference -> ProjectSemantic -> OutputGate
```

规则依赖形成小型 DAG，但同一阶段按稳定的 rule ID 排序。转换失败的字段标记为 `Invalid`，依赖该字段的后续规则跳过并只保留根因；目标表失败时跨表引用报告“目标不可验证”，不能同时误报每个 key 不存在。

#### 不足四：诊断设想超过现有模型，但缺少兼容迁移

当前 `Diagnostic` 只有 severity、file、sheet、cell、message。初稿直接要求 code、field、related locations 等字段，没有说明 JSON 兼容、控制台格式和调用方迁移，也没有线程安全约束；而生成器会并发处理输入文件，collector 中普通 `List` 不能被项目级并行写入。

**修正建议：** 先版本化诊断 DTO：

- 保留现有字段和构造路径，新增可选 `Code`、`Table`、`Field`、`RuleId`、`SourceSpan`、`RelatedLocations`；
- JSON 报告增加 `formatVersion`，新增字段只追加、不重命名旧字段；
- collector 改为线程安全 sink，最终统一去重、限流和稳定排序；
- 诊断 message 面向人，自动化只依赖 code 与结构字段，测试不再只匹配整段文案。

#### 不足五：规则来源、优先级和版本没有闭环

初稿同时允许内建规则、程序化规则、Rules Sheet、JSON/YAML，却没有定义同名 rule 合并、禁用或冲突时的语义，也没有说明规则是否影响生成 fingerprint 和 schema hash。这会使 CLI、MSBuild、Unity 使用不同规则集，甚至出现相同输入生成结果不同。

**修正建议：** 第一版只支持两种来源：内建不可关闭规则，以及显式注册的项目规则。声明式格式在 ADR 冻结前只做实验，不同时支持 Excel、JSON 和 YAML。每条规则具有稳定 ID、规则实现版本、参数规范值和来源；它们共同计算 `ValidationFingerprint`。业务规则默认不进入运行时 `SchemaHash`，因为不改变二进制布局，但必须进入增量 manifest 和诊断报告。只有真正改变字段或编码布局的规则才进入 schema hash。

#### 不足六：项目级校验没有定义“生成全集”

跨表引用可能指向被 tag 过滤、增量跳过、命名子表、同名不同 namespace 或本次命令未选择的输入。初稿提出“目标不存在/未加载”区分，但缺少确定的表身份与解析范围，无法实现一致结果。

**修正建议：** 使用规范 `TableId(namespace, className, childName)`，并在生成开始时先构造 project catalog。catalog 中表状态至少区分 `Selected`、`Filtered`、`Unavailable`、`Invalid` 和 `Valid`。默认严格模式只允许引用本次 catalog 中的 `Valid` 表；需要引用外部发布表时，必须显式提供带 schema/version 的 manifest，不能把“未找到”当作成功。

#### 不足七：全量快照可能带来不可控内存成本

初稿要求所有表完成后再运行项目规则，但未考虑大型 workbook、matrix 展开和并行生成。把所有解析对象常驻内存可能显著放大峰值，尤其复合值和稀疏矩阵。

**修正建议：** snapshot 采用按表所有权和确定性释放：单表 pass 完成后仅保留项目规则声明所需的投影索引，例如外键目标 key set，而不是保留全部行。matrix 保持稀疏项，不物化默认值。增加预算指标（行数、值数、投影字节估算），超过可配置阈值时给出明确错误，不以未经测量的性能结论作为设计依据。

#### 不足八：自定义 validator 的公共扩展边界过早

如果直接把 `GenerationContext`、NPOI 或内部 `DataTypeDescriptor` 暴露给第三方程序集，未来重构会形成兼容包袱；动态加载 validator 还涉及依赖解析、异常隔离和 Unity 环境差异。

**修正建议：** P0/P1 的接口保持 `internal`，先完成内建六类型并验证模型。公共扩展 API 单独做 ADR，使用只读、版本化 DTO，不暴露 NPOI；CLI/MSBuild 的插件加载和 Unity 的静态注册分别设计。自定义规则异常必须转换为单条基础设施诊断，并标记整个规则失败，不能中断其他无依赖规则。

#### 不足九：没有明确兼容性基线和发布策略

“有效 fixture 不变化”不足以覆盖错误行为变化。把原本生成时抛出的错误提前到 validate、一次报告多个错误、开始校验唯一索引，都可能让历史上能导出的表停止生成。

**修正建议：** 建立规则兼容等级：

- `Required`：现有格式/结构不变量，立即作为 Error；
- `Recommended`：新合理性规则，首个版本默认 Warning；
- `ProjectPolicy`：项目显式启用并选择 severity。

每个新增内建规则必须记录旧行为、启用版本、默认 severity 和修复方式。发布说明提供审计模式，先输出报告但不阻止生成；一个迁移周期后才能将已公告规则升级为 Error。

## 3. 校验分层

校验应拆成四层，避免把“格式正确”误当成“业务正确”。

| 层级 | 典型规则 | 执行时机 | 失败默认级别 |
| --- | --- | --- | --- |
| Schema | 必需字段、字段名、类型、索引声明 | 单表 Schema 解析后 | Error |
| Cell | 值可解析、必填、范围、长度、正则、集合 | 数据行读取后 | Error |
| Table semantics | 唯一性、顺序、tree/graph/matrix 结构约束 | 单表全部行读取后 | Error/Warning |
| Project semantics | 外键、跨表唯一、成对配置、版本一致性 | 所有逻辑表读取后 | Error/Warning |

各层共同遵守：

- validator **只读** Schema 和数据，不修正、裁剪或补齐用户输入；
- parser 负责解释布局，validator 负责判断其结果是否有效，writer 只负责确定性输出已验证的数据；
- 可恢复的问题写入 `DiagnosticsCollector` 后继续收集，同一规则可设置每表最大问题数，防止错误洪泛；
- 基础设施故障或内部不变量破坏才抛异常，用户数据错误不依赖异常作为正常控制流。

## 4. 修订后的内部扩展点

本节代码只描述职责，不承诺公共 API。P0/P1 先以 `internal` 实现；是否开放第三方 validator 必须等待 DTO、版本和加载模型 ADR 通过。

### 4.1 单表 validator

```csharp
internal interface ITableValidationPass
{
    string RuleId { get; }
    ValidationPhase Phase { get; }
    ValidationPassResult Validate(TableValidationContext context);
}

internal sealed class TableValidationContext
{
    public TableSchemaSnapshot Schema { get; }
    public TableDataSnapshot Data { get; }
    public ValidationProfile Profile { get; }
    public IDiagnosticSink Diagnostics { get; }
}
```

`TableDataSnapshot` 包含布局无关的逻辑记录，而不是直接暴露 NPOI `IRow`：

```csharp
internal sealed class ValidationRow
{
    public int LogicalIndex { get; }
    public IReadOnlyDictionary<string, ValidationValue> Values { get; }
}

internal sealed class ValidationValue
{
    public string RawText { get; }
    public ParsedValueState State { get; }
    public DataTypeDescriptor Type { get; }
    public object? CanonicalValue { get; }
    public SourceSpan Source { get; }
}
```

这样 `table` 的 Excel 行、`column` 的 Excel 列、`matrix` 展开的稀疏坐标项以及 `kv` 的键值项都能进入相同规则引擎，同时保留类型、四态解析结果和精确位置。规范值由无状态 materializer 产生一次，validator 与 serializer 共享结果，避免相同文本被两套逻辑解释。具体实现应优先使用按类型封装的不可变值，`object?` 只是迁移期占位，不能作为公开契约。

### 4.2 注册与组合

校验 pass 不建立独立的顶层 registry，而是挂到统一 `TableTypeDescriptor`，键使用规范化后的 `DTGen` 类型。默认描述符包含：

```text
table  -> RowMaterializer    + RowTableValidator
column -> ColumnMaterializer + ColumnTableValidator
matrix -> MatrixMaterializer + MatrixTableValidator
kv     -> KvMaterializer     + KvTableValidator
tree   -> RowMaterializer    + TreeTableValidator
graph  -> RowMaterializer    + GraphTableValidator
```

每个类型 validator 是组合入口，而不是复制全部规则。建议执行顺序为：

1. `CommonCellRulesValidator`：类型可解析、必填和值约束；
2. `CommonIndexValidator`：唯一索引、分组键与稳定顺序；
3. 类型 validator：布局和结构语义；
4. 用户声明的字段/表级规则。

组合 registry 应拒绝重复类型，并列出支持类型。缺失 materializer、validator、serialization plan 或 renderer 都必须产生启动错误，不能静默回退到 `table`。过渡期若尚未迁移全部组件，架构测试必须核对旧注册点集合，且由组合 registry 输出唯一的 supported/reserved 清单。

### 4.3 项目级 validator

跨表引用需要第二个扩展点：

```csharp
internal interface IProjectValidationPass
{
    string RuleId { get; }
    ValidationPassResult Validate(ProjectValidationContext context);
}
```

`ProjectValidationContext` 包含生成开始时建立的 catalog、有效表的只读投影索引，以及按稳定 `TableId(namespace, class, child)` 建立的目录。项目规则仅在单表解析完成后运行；若被引用表被过滤、不可用或校验失败，诊断必须准确报告 catalog 状态，不能假定引用有效。

## 5. 各表类型的第一阶段规则

### 5.1 `table`

- 所有非忽略字段的值均可按声明类型解析；
- 显式 `required` 字段不接受空单元格；
- 每个唯一 `Index` 的字段组合在有效行中唯一，错误同时指出首次出现行和重复行；
- `Group` 只验证键可用，不要求唯一；
- 可选字段规则：数值范围、字符串长度/正则、允许值集合、顺序连续或单调；
- 显式外键规则验证目标表及目标唯一键存在。

空白整行和 `#` 注释行沿用 reader 的有效行判定，所有 validator 与 writer 必须共享该判定结果。

### 5.2 `column`

逻辑记录按 Excel 列构造，复用 `table` 的字段和值规则，但诊断位置映射为字段所在行与记录所在列。另需验证：

- 注释列不会进入唯一性或引用校验；
- 非空逻辑记录缺失 required 字段时报对应交叉单元格；
- 标题/类型列自身不被误识别为数据记录。

### 5.3 `matrix`

- Key1、Key2 坐标都能按声明类型解析且各自唯一；
- 展开后的 `(Key1, Key2)` 组合唯一；
- 值能按 Value 类型解析，并可应用范围/集合规则；
- `MatrixDefaultValue` 只表示“跳过该项”，其文本若与合法业务值冲突应给出 Warning；
- 可选规则可要求完整矩阵、方阵、对称、禁止对角项或指定稀疏度，但这些都必须显式声明，不能成为默认假设。

### 5.4 `kv`

- Key 必填、符合 C# 成员命名规则且唯一；
- Type 必填且合法；Value 必填并能按该行 Type 解析；
- 每个 Key 可附带数值范围、集合或正则规则；
- 如果生成强类型成员名经过规范化，规范化后的冲突也必须报错；
- JSON 只在声明了 `json<T>` 时验证语法和目标类型结构，不把普通字符串猜测为 JSON。

### 5.5 `tree`

- Id 必填且唯一，ParentId 为空表示根；
- 非空 ParentId 必须引用同表节点，节点不得引用自身；
- 整棵树无环，环诊断给出完整路径和相关行；
- Order 存在时必须可解析，并可选择在同一父节点下唯一或连续；
- 根节点数量、最大深度、叶子要求属于可选项目规则，不默认限制。

现有 `DataRowBinarySerializer.ValidateTreeRows` 的逻辑应迁移到 `TreeTableValidator`，writer 不再承担验证。

### 5.6 `graph`

- EdgeId 必填且唯一，From/To 必填；
- Weight 存在时必须可解析，并可显式限制为非负或指定范围；
- 是否允许自环、平行边、无向重复边、悬空节点必须由表级规则明确；
- DAG 模式才执行有向环检测，普通 graph 不应默认禁止环；
- 若节点来自外部节点表，则 From/To 使用项目级外键校验。

现有 `DataRowBinarySerializer.ValidateGraphRows` 的逻辑应迁移到 `GraphTableValidator`。

## 6. 规则声明方案

建议分两阶段落地，先解决架构和内建语义，再引入稳定的声明语法。

### 阶段一：现有元数据 + 程序化规则

- 从 `Index`、`Group`、字段类型和 `DTGen` 推导无歧义的内建规则；
- 内部实现内建 `ITableValidationPass` / `IProjectValidationPass`，暂不承诺公共插件 API；
- 不根据字段名猜规则，不立即引入表达式语言。

### 阶段二：声明式规则

候选载体包括独立 `Rules` Sheet 或外部规则文件，但实现前必须通过 ADR 只选一种首发格式，而不是同时支持多套语法。无论最终载体如何，规则的规范模型可以表示为：

| Table | Field | Rule | Arguments | Severity | Code |
| --- | --- | --- | --- | --- | --- |
| Item | Price | range | `min=0,max=999999` | error | ITEM_PRICE_RANGE |
| Quest | RewardItemId | reference | `Item.Id` | error | QUEST_REWARD_REF |
| SkillTree | Order | uniqueWithin | `ParentId` | warning | SKILL_ORDER_DUP |

第一批规则限定为 `required`、`range`、`length`、`regex`、`oneOf`、`unique`、`reference`、`sequential`。规则参数需要自己的严格 parser、版本号和诊断；未知规则或参数必须报错。暂不支持任意表达式，以避免执行安全、跨语言一致性和错误定位问题。

## 7. 诊断契约

建议校验诊断码以 `DTV` 开头并保持稳定：

| 范围 | 示例 | 含义 |
| --- | --- | --- |
| DTV1xxx | `DTV1001` | 单元格缺失或无法解析 |
| DTV2xxx | `DTV2001` | 唯一性、范围、顺序等单表规则 |
| DTV3xxx | `DTV3001` | tree/graph/matrix 类型语义 |
| DTV4xxx | `DTV4001` | 跨表引用或项目规则 |
| DTV9xxx | `DTV9001` | 规则配置错误或 validator 缺失 |

每条诊断至少包含 `severity`、`code`、`message`、`file`、`sheet`；能定位时增加 `table`、`field`、`cell`、`logicalRow`、`rule` 和 `relatedLocations`。例如重复 Id 的主位置指向第二处，related location 指向第一处。

聚合策略：

- 同一规则默认最多报告 100 条，之后追加一条截断 Warning；
- 输出按文件、Sheet、单元格、规则码稳定排序，保证 CI diff 可读；
- `validate --diagnostics-json-output` 输出结构化数据，控制台输出简短修复提示；
- `--warnings-as-errors`（后续选项）只改变退出结果，不改变原始 severity，便于工具消费。

## 8. 建议生成流程

```text
1. 发现输入与逻辑表
2. parser 生成 schema + layout
3. schema validators 收集问题
4. materializer 构造 TableDataSnapshot
5. 单表 data validators 收集问题
6. 单表完成后保留所需投影；catalog 就绪后运行 project validators
7. 有 Error：输出报告并终止事务
8. 无 Error：从同一份已解析值生成代码与 .bytes
```

`ValidateOnly` 与正常生成必须走 1–6 的完全相同路径；区别仅在第 8 步是否提交产物。增量生成不能仅凭输入未变就跳过项目级校验，因为被引用目标可能改变；manifest 需要记录 validation fingerprint、目标投影摘要与引用依赖，未实现这些记录时必须保守地重新运行项目规则。

## 9. 分阶段实施与验收

### P0：行为基线与观测（不改变生成结果）

1. 用 characterization tests 固定六种类型当前的成功产物、失败时机和错误位置；
2. 为现有 `Diagnostic` 设计向后兼容的 v2 DTO 与 JSON `formatVersion`；
3. 建立唯一 supported/reserved 类型清单，并增加注册完整性架构测试；
4. 记录生成阶段、峰值逻辑行/值数量和诊断数量，作为 snapshot 方案的预算基线；
5. 建立 `ValidateOnly` 漏检 tree/graph 错误的失败测试，但此阶段不移动职责。

验收：现有行为被自动化刻画；诊断格式可兼容演进；支持类型清单不存在重叠或漂移；没有新增默认 Error。

### P1：统一 materialization 与既有规则迁移

1. 引入内部 `TableDataSnapshot`、四态值、`SourceSpan` 和统一无状态类型转换；
2. 让 serializer 消费规范值，先用双路径对照测试证明新旧 bytes 一致，再删除重复转换；
3. 通过 `TableTypeDescriptor` 聚合 parser、materializer、validation passes、serialization 和 renderer；
4. 将 tree/graph 既有规则从 serializer 迁出，使 `ValidateOnly` 与正常生成执行同一路径；
5. 诊断 sink 支持根因抑制、限流、去重和稳定排序。

验收：所有既有有效 fixture 的代码与 bytes 完全一致；tree/graph 的同一错误在两种生成模式产生相同 code 和位置；writer 中不再包含类型语义判断；快照峰值受预算保护。

### P2：通用单表规则，以审计模式发布

1. 增加 required、range、oneOf、regex、unique、sequential 的内部 rule definitions；
2. 唯一索引报告首次位置与冲突位置，并抑制依赖无效值的派生错误；
3. 新合理性规则默认 Warning/审计模式，记录规则兼容等级与启用版本；
4. 计算 `ValidationFingerprint` 并写入增量 manifest 和诊断报告；
5. 覆盖 table/column/matrix/kv/tree/graph 的成功、根因失败和诊断快照测试。

验收：一次 validate 能报告多个独立根因；每个问题可定位到 Excel 单元格；审计模式不改变历史生成结果；显式严格模式下 Error 不提交任何产物。

### P3：跨表语义与可选扩展 API

1. 在输入发现阶段建立 `TableId` catalog 和表状态机；
2. 只保留项目规则所需的唯一键/引用投影，实现跨 Sheet、跨文件 reference；
3. 将标签、增量依赖、外部 manifest 和规则 fingerprint 纳入失效策略；
4. 通过 ADR 冻结单一声明式格式；评估后再决定是否发布版本化插件 DTO；
5. CLI、MSBuild、Unity 分别验证一致的规则发现、配置与诊断结果。

验收：缺失、过滤、不可用、无效目标和缺失 key 产生不同诊断；规则或目标投影变化会使引用方重新校验；未启用插件时不存在动态程序集加载。

## 10. 测试矩阵

- **注册测试**：parser、validator、writer/template 支持类型集合一致，重复和未知注册失败；
- **布局测试**：同一 required/unique 规则在 table 与 column 中映射到正确单元格；
- **类型测试**：基础、enum、array、map、json、custom 的成功/失败解析与空值语义；
- **结构测试**：matrix 重复坐标，tree 缺父节点/自引用/多节点环，graph 空端点/重复边/DAG 环；
- **项目测试**：跨 Sheet/文件引用、标签过滤目标、目标生成失败、复合键；
- **事务测试**：Error 不落盘，Warning 正常生成，ValidateOnly 无副作用；
- **诊断快照**：文本与 JSON 的 code、位置、related locations 和稳定顺序；
- **回归测试**：有效 fixture 生成的 C# 与 `.bytes` 保持确定性且成对更新。

## 11. 待决策问题

在进入对应 P1–P3 工作项前需要明确：

1. 空字符串、空白字符串与缺失单元格在 `string` 和可空类型中是否等价；
2. 数值范围使用源类型比较还是统一 decimal，比对浮点特殊值时如何处理；
3. 跨表标识是否包含 namespace、命名表名和 tag 视图；
4. Warning 严格化采用全局开关还是允许按规则码配置；
5. 自定义 validator 的程序集加载、安全边界和 CLI/MSBuild/Unity 一致性；
6. 声明式规则文件是否纳入生成 fingerprint，以及如何记录跨表依赖。

在这些语义冻结前，P0 只建立行为与观测基线；P1 仅迁移已有、无歧义的规则，不引入会改变现有合法数据含义的默认限制。
