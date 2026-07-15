# DataTables Agent 工程评审与优化计划

> 评审日期：2026-07-14  
> 范围：`AGENTS.md`、`CLAUDE.md`、README、规划文档、测试与公开 API 元数据。  
> 结论：当前 Agent 工程方向总体正确，能帮助编码 Agent 快速理解项目；但存在“宣传性描述高于可验证事实”“多份 Agent 文档漂移”“历史状态未及时归档”“缺少任务路由与验收标准”的问题。建议把 Agent 文档从项目介绍型文档升级为“事实锚定、任务路由、风险约束、验证优先”的工程操作手册。

## 1. 信息来源与评估方法

### 1.1 仓库内来源

- `AGENTS.md` 与 `CLAUDE.md`：当前 Agent 指令主体。
- `README.md`：面向用户的当前项目事实基线。
- `docs/planning/optimization-plan-2026-07.md`：上一轮架构评审与二期优化计划。
- `tests/DataTables.Tests/*`：用于验证运行时 API、并发、缓存、数据源管线、预热与公开 API 的实际落地情况。

### 1.2 外部同类项目与实践参考

本次调研重点关注游戏配置表、Excel 到代码/数据生成、Unity/.NET 运行时加载相关项目。外部实践对本项目的启发主要包括：

- Luban 的命令行生成模式强调明确的输入、输出、目标语言、数据格式和“只校验不输出”的 saver，这对 DataTables 的生成器诊断、CI 校验模式和产物一致性检查有参考价值。
- Unity 生态中的 Excel-to-Unity 类项目通常覆盖 ID、常量、本地化、JSON 或 ScriptableObject 输出，说明 DataTables 文档应把“强类型 C# + `.bytes` 同步发布”的差异化约束讲清楚。
- MiniExcel 等 .NET Excel 处理库强调轻量、低内存和大数据性能，这提示 DataTables 的 Agent 文档在涉及性能收益时应引用项目内 benchmark 或测试，而不是使用无法复现的百分比口径。

### 1.3 历史对话记录限制

当前仓库没有可查询的历史对话归档，也没有 `.codex`、`.claude` 或类似目录记录过往 Agent 决策。因此“历史对话记录”只能通过现有文档中的历史痕迹间接推断：`AGENTS.md` / `CLAUDE.md` 保留了早期“激进优化版”表述，而 README 与规划文档已经更偏向 v3、数据源管线、上下文隔离和强制迁移策略。后续若希望 Agent 能复盘历史决策，应显式建立 ADR 或 Agent 决策日志。

## 2. 当前 Agent 工程合理性评估

### 2.1 合理之处

1. **覆盖面较完整**：当前 Agent 指令包含项目定位、核心组件、生成器结构、常用命令、Unity 说明、测试命令和历史兼容 API，对新 Agent 有较好的启动帮助。
2. **核心方向与代码基本一致**：异步加载、`GetCached`、LRU 估算缓存、`IDataSource`、`PreheatAsync`、Hook、并发安全等能力在测试与 README 中均能找到对应证据。
3. **面向生成器和运行时双域**：文档同时描述 Excel 生成链路与运行时加载链路，符合 DataTables 既是生成器又是运行时库的项目特点。
4. **保留兼容信息**：旧回调 API、`GetDataTable*`、`CreateDataTable*` 等兼容入口已经进入破坏性清理范围，后续 Agent 应避免恢复这些冗余入口。

### 2.2 不准确或容易误导的内容

| 问题 | 现状 | 风险 | 建议 |
| --- | --- | --- | --- |
| 性能收益口径过强 | `30-50%内存优化`、`90%性能提升潜力`、`零延迟` 等表述缺少基准链接 | Agent 可能把目标当成已验证事实，继续扩写宣传性代码或文档 | 改成“目标/预期/需 benchmark 证明”，并链接 `docs/performance` 或 benchmark 命令 |
| API 名称漂移 | Agent 文档示例使用 `EnableMemoryManagement(50)`，README 强调 `EnableEstimatedMemoryBudget(50)` | Agent 可能在新代码中偏向旧别名或不准确命名 | Agent 文档应以 README 当前推荐 API 为准，不再提供旧别名示例 |
| 数据源抽象描述过时 | `IDataSource` 被描述为“文件系统、网络、自定义数据源”，但当前 README 已强调可组合装饰器与 payload 缓存边界 | Agent 对数据源管线的缓存/压缩/版本边界理解不足 | 增加“数据源管线任务必须先看 docs/guides/data-source-pipeline.md” |
| 生成器架构描述偏粗 | 只描述 T4、并行 Excel、二进制序列化，缺少 v3 schema hash、结构化 header、注册式 parser 等当前重点 | Agent 修改生成器时容易绕开新分层 | 增加 Schema/Validation/Serialization/Diagnostics 任务路由 |
| Unity 镜像风险不足 | README 明确 `src/DataTables` 是源，Unity 镜像由构建同步；Agent 文档只说 Unity 路径 | Agent 可能直接编辑 Unity 镜像 | 明确禁止直接编辑 Unity runtime 镜像，除非同步机制本身变更 |
| 缺少 T4 专项要求 | 项目有 T4 生成模板与生成物，但 Agent 文档没有“先改 `.tt` 再生成 `.cs`” | Agent 可能直接改生成代码，造成模板漂移 | 加入 T4 任务必须遵循模板源优先和生成检查 |
| 缺少 PR/验收格式 | 目前只有命令清单，没有按任务类型规定最小测试集 | 变更质量不稳定 | 增加 runtime/generator/docs/templates 的最小验证矩阵 |

## 3. 内容是否有偏移

当前 Agent 文档的主要偏移不是架构方向错误，而是**历史营销化表达尚未被工程化事实替换**：

- README 已经把运行时描述收敛为异步数据源读取、单飞加载、独立 `DataTableContext`、可选 LRU 表缓存、可组合数据源装饰器；Agent 文档仍突出“激进优化”“90%性能提升潜力”等口号。
- 规划文档已经把下一阶段重点放在 v3 格式稳定、`DataTableProcessor` 职责收口、显式表注册、数据源 manifest、索引诊断、类型处理器无状态化；Agent 文档尚未把这些变成具体操作约束。
- README 强调代码和 `.bytes` 必须同源生成、同步发布；Agent 文档虽有生成流程，但没有把“产物一致性”作为强约束。

因此，Agent 工程应从“告诉 Agent 项目很快、很现代”转向“告诉 Agent 哪些事实已落地、哪些仍是计划、修改哪类文件要走哪条验证路径”。

## 4. 同类项目最佳实践映射

### 4.1 生成器 CLI 与 CI 校验

同类配置生成器通常把生成任务参数化为输入目录、输出目录、目标语言、数据格式、代码格式、校验模式。DataTables 已具备 CLI 和 MSBuild 集成，但 Agent 文档应补充：

- 生成器相关变更必须覆盖 CLI、MSBuild 和 sandbox 示例至少一个端到端路径。
- 支持或规划 `validate-only` / null output 模式，用于 CI 中只校验 Excel/schema，不写产物。
- 生成的 C# 与 `.bytes` 是同一 schema 的配套产物，不能只更新一边。

### 4.2 表类型与扩展点注册

成熟的配置表工具通常把 table、kv、matrix、localized 等表类型隔离为可注册解析/校验/输出单元。DataTables 已在规划中明确 parser/validator/writer/template 分层，Agent 文档应要求：

- 新增表类型不得直接扩大 `DataTableProcessor` 主流程。
- 新类型必须同时补齐 parser、validator、writer/template、文档和测试。
- 诊断必须定位到文件、Sheet、字段或单元格。

### 4.3 运行时加载与缓存边界

Unity 与服务端双运行时的核心差异在于线程、I/O、平台文件系统和内存预算。Agent 文档应强调：

- 新运行时 API 优先使用 `LoadAsync` / `GetCached` / `IsLoaded`。
- 单次调用方取消不能破坏共享加载；修改相关代码必须跑并发与取消测试。
- `EnableEstimatedMemoryBudget` 是估算缓存预算，不是进程内存硬上限。
- 数据源 payload 缓存与已解析表缓存是两层缓存，不能混淆。

### 4.4 文档与决策记录

同类工程的长期可维护性依赖 ADR、迁移指南和兼容策略。DataTables 已有迁移文档和规划文档，但缺少 Agent 可消费的决策索引。建议新增：

- `docs/adr/`：记录 v3 强制迁移、数据源 flags 语义、显式注册优先等长期决策。
- `docs/planning/`：保留阶段计划，但每份计划标注状态：proposed / active / completed / superseded。
- `AGENTS.md`：只放稳定事实、任务路由和验证矩阵，减少大段项目介绍。

## 5. 优化计划

### 阶段 A：修正 Agent 文档事实基线（P0）

1. **合并或明确 `AGENTS.md` 与 `CLAUDE.md` 的职责**
   - 推荐：`AGENTS.md` 作为唯一事实源，`CLAUDE.md` 改为短文件，指向 `AGENTS.md`。
   - 验收：两者不再长期复制大段内容，避免漂移。

2. **把宣传性性能表述降级为可验证目标**
   - 删除或改写“30-50%”“90%”“零延迟”等无基准引用说法。
   - 若保留，必须链接 benchmark 或性能文档。
   - 验收：Agent 文档中性能结论均能追溯到测试、benchmark 或明确标注为目标。

3. **同步当前推荐 API**
   - 主示例使用 `EnableEstimatedMemoryBudget`。
   - 删除 `EnableMemoryManagement`、旧回调 API、`GetDataTable*` 等兼容章节。
   - 验收：新代码示例不再优先展示旧别名。

4. **补充 Unity 镜像与 T4 约束**
   - 明确 `src/DataTables` 是 runtime 修改源。
   - 修改 `.tt` 必须先改模板并重新生成对应 `.cs`。
   - 验收：Agent 在相关任务中不会直接把生成物当源头。

### 阶段 B：建立任务路由和验证矩阵（P0）

在 `AGENTS.md` 中加入“按任务类型执行”的最小规则：

| 任务类型 | 必读文件 | 最小验证 |
| --- | --- | --- |
| Runtime 加载/缓存/并发 | README 推荐 API、`docs/guides/data-source-pipeline.md`、相关 tests | `dotnet test tests/DataTables.Tests/DataTables.Tests.csproj --filter "DataTableContextTests|ConcurrencyTest|LRUDataTableCacheTest"` |
| 生成器/schema | `docs/guides/table-types.md`、`docs/reference/binary-format-v3.md`、规划文档 | `dotnet test tests/DataTables.Tests/DataTables.Tests.csproj --filter "Generator|Schema|Binary"`，必要时跑 sandbox |
| T4 模板 | 对应 `.tt` 与 LastGenOutput 生成物 | 重新生成模板输出并 `dotnet build DataTables.sln` |
| 文档 | README、docs index、相关设计文档 | `dotnet build DataTables.sln` 可选；检查链接与示例 API |
| Unity | README Unity 章节、Unity runtime 镜像说明 | 只改源 runtime 后验证同步；必要时检查 Unity 镜像差异 |

### 阶段 C：引入 Agent 决策记录（P1）

1. 新建 `docs/adr/`，优先记录：
   - v3 二进制格式强制迁移，不做 v2 兼容读取。
   - 数据源 flags 暂按传输层装饰器模型处理，payload flags 保留。
   - 显式表注册优先，反射扫描仅兼容兜底。

2. 新建 `docs/planning/README.md` 或在现有 docs index 中加入计划状态说明：
   - active：当前正在执行。
   - completed：已落地。
   - superseded：被新计划替代。

3. 新增 Agent 历史日志可选模板：
   - 背景、决策、影响文件、验证命令、回滚方式。

### 阶段 D：持续校验 Agent 文档准确性（P1）

1. **公共 API 漂移检查**
   - 继续维护 `PublicApiMetadataTests`。
   - 当 README 或 AGENTS 示例引用 API 时，优先确认测试存在或编译示例存在。

2. **文档示例编译化**
   - 将关键 README/AGENTS 示例抽取到测试 fixture 或 sandbox。
   - 防止示例 API 名称滞后。

3. **生成产物一致性检查**
   - CI 增加生成器端到端检查，确保 `.tt`、生成 `.cs`、`.bytes` 与 schema hash 不漂移。

## 6. 建议后的 AGENTS.md 目标结构

建议下一步将 `AGENTS.md` 改造成以下结构：

1. 项目事实基线：一句话定位、生成物同步约束、运行时推荐 API。
2. 当前稳定能力：只写已落地事实，避免无基准百分比。
3. 任务路由：runtime / generator / T4 / docs / Unity。
4. 禁止事项：直接改 Unity 镜像、直接改 T4 生成物、绕过 v3 schema hash、扩大 `DataTableProcessor` 主流程。
5. 验证矩阵：按任务类型给最小命令。
6. 规划入口：链接 active planning 与 ADR，而不是在 Agent 文档内复制长篇计划。

## 7. 优先级排序

| 优先级 | 工作项 | 价值 | 成本 |
| --- | --- | --- | --- |
| P0 | AGENTS/CLAUDE 去重与 API 事实修正 | 立即降低 Agent 误导风险 | 低 |
| P0 | 增加任务路由与验证矩阵 | 提升每次 Agent 改动质量 | 低 |
| P0 | 增加 T4 与 Unity 镜像硬约束 | 避免高风险错误修改 | 低 |
| P1 | ADR 与计划状态索引 | 改善历史决策可追溯性 | 中 |
| P1 | 文档示例编译化 | 长期防漂移 | 中；已通过 `DocumentationSmokeTests` 覆盖 README 与 AGENTS C# 示例 |
| P2 | validate-only 生成器/CI 模式 | 提升产品化质量 | 中高；已通过 `GenerationMode.ValidateOnly`、CLI `validate` 子命令与 MSBuild `ValidateOnly` 属性落地 |

## 8. 总体结论

当前 Agent 工程并非方向错误，而是需要从“历史优化宣传稿”升级为“可验证工程手册”。最重要的修正是：

- 用 README 和测试中已落地的事实替代过度承诺。
- 用任务路由和验证矩阵指导 Agent 行为。
- 用 ADR/计划状态承接历史对话和架构决策。
- 用 T4、Unity 镜像、v3 强制迁移等硬约束保护项目边界。

完成上述调整后，Agent 更可能沿着 DataTables 的真实架构演进，而不是被早期口号或过时示例带偏。
