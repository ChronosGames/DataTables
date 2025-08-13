* [x] 模式解耦
  * 将三种模式解析拆分为 ITableSchemaParser 接口 + RowTableParser、ColumnTableParser、MatrixTableParser 实现，CreateGenerationContext 只负责选择解析器。
* [~] NPOI 抽象层
  * 增加 ISheetReader/IRowReader/ICellReader 抽象，提供 NpoiSheetReader 与 CsvSheetReader，后续可无痛接入 CSV 或流式解析。
  * 已切换三种解析器为基于 ISheetReader 的实现；CSV Reader 与单测已添加。
* [x] 解析参数化
  * 引入 ParseOptions（严格/宽松、是否校验公式、日期/数字格式、标签过滤规则、注释标记可配置等），减少魔法常量耦* 合。
* [~] 诊断与可观测性
  * 新增 DiagnosticsCollector 聚合 Warning/Error，并精确到 File/Sheet/Cell（A1 样式）。
  * 输出解析/生成耗时、被忽略字段/列、Tag 命中统计，可选生成 JSON 报告。（已支持 JSON 报告；统计项：待补充）
* 文化与格式统一
  * 区域性与小数点
    * 所有数字/日期解析与格式化统一使用 `CultureInfo.InvariantCulture`。
    * 仅支持小数点 `.` 作为小数分隔符；不支持千分位分隔符。
    * 数值允许前导/后导空白并自动裁剪。
  * 布尔值规范
    * 允许（大小写不敏感）：`1/0`、`y/n`、`yes/no`、`true/false`、`true()/false()`；输出统一为 `true/false`。
  * 日期时间规范
    * 输入推荐格式（24小时制，严格校验）：`yyyy-MM-dd`、`yyyy-MM-dd HH:mm:ss`、`yyyy-MM-ddTHH:mm:ss`。
    * 解析失败时输出明确报错信息，包含原始值与期望格式提示。
    * 二进制序列化使用 `Ticks` 存储；代码生成时以构造参数形式还原（年、月、日、时、分、秒）。
  * 枚举值规范
    * 源数据需严格匹配枚举名称（区分大小写）；不支持数字枚举值直填。
    * 解析失败时提示合法取值集合（友好诊断）。
  * 空值策略（遵循当前处理器实现）
    * 数值型（`int/long/double/float`）：空值视为 `0`。
    * `bool`：空值视为 `false`。
    * `DateTime`：空值报错（需提供清晰错误信息）。
    * `decimal`：空值报错（不接受空字符串）。
  * 字符串与 JSON
    * 字符串默认裁剪两端空白；换行规范化为 `\n`。
    * `JSON` 字段需为严格 JSON，解析采用 Invariant 文化，不受本地化影响。
* 性能微优化
  * 预扫描 UsedRange（首尾裁剪空行/列）。
  * 缓存 DataProcessor（按 TypeName）避免重复查找。
  * Column 模式预构建“有效字段行列表”，列写出前先判空，减少 GetRow/GetCell 次数。
* [~] 公式评估策略
  * 增加开关：关闭/仅校验/强制评估，评估失败回退为缓存值并记录告警。（已支持策略开关与校验；强制评估/回退策略后续完善）
* 错误体验
  * 对“不存在的类型/索引字段/分组字段”提供“建议修复”（最相近名字、合法类型列表）。
  * 对非法 Name 提供宽松模式：自动清洗为合法标识符并记录告警。
* [x] 注释/过滤增强
  * 注释标记文本（如 #行注释标志、#列注释标志）支持配置；Tag 过滤规则支持配置与多标签布尔表达式（AND/OR/NOT，支持括号、&&/||/!）。
  * 标签过滤语法（TFE Lite）：极简且与现有实现一致。
  * 语法：IDENT（`[A-Za-z0-9_]+`）、AND/OR/NOT（别名 `&&`/`||`/`!`）、括号 `()`；优先级 `NOT > AND > OR`；空表达式为 `true`（不过滤）。
  * Excel 标签：在列标题末尾以 `@` 标注标签；多个标签用非单词字符分隔（空格、逗号、分号、竖线、中文逗号等）。示例：`Hp@CLIENT`、`Atk@CLIENT,SERVER`。
  * 错误提示：括号不匹配（Unmatched parenthesis）、表达式非法（Invalid tag filter expression）。
  * 兼容性：100% 兼容当前实现，无需代码改动，仅需补充文档与示例。
* API/测试友好
  * 提供 TryParseGenerationContext(ISheet, ParseOptions, out GenerationContext, out Diagnostics) 静态方* 法，便于单测与复用。
  * 为三模式分别增加黄金用例与边界用例测试。
* [~] 清理与常量集中
  * 移除 m_FileStream/m_BinaryWriter 等未使用字段。（进行中）
  * 将魔法字符串/默认值集中至 Constants 或 ParseOptions。（进行中）

* [x] CLI参数与文档
  * 在 CLI 中暴露 ParseOptions，并支持输出诊断 JSON
  * 在 README 增加解析选项与诊断的使用示例

* 单测计划
  * [x] 解析器黄金用例（Row/Column/Matrix 基本路径）
  * [ ] 边界用例：空行/空列、非法 Name 宽松模式、列注释行跳过、Matrix 默认值跳过逻辑
* 大表场景
  * .xlsx 可选基于 XSSF 事件模型（SAX）流式读取，降低内存占用；CSV 提供专用快速路径。
