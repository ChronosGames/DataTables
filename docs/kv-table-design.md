# kv 表类型设计

`kv` 是已实现的键值配置表类型，面向全局参数、功能开关、少量散列配置和不适合拆成多行记录的小型配置集合。当前生成器会解析 `DTGen=kv`，生成强类型静态属性，并提供 `TryGetValue<T>` / `GetValue<T>` 动态读取 API。

## 目标

- 用统一格式表达全局参数和功能开关，避免为少量配置创建大量只有一行的数据表。
- 复用现有 DataTables 类型系统，支持基础类型、枚举、数组、JSON 和自定义类型。
- 生成强类型只读属性，减少业务代码中的字符串 key 查询。
- 在生成期发现重复 key、非法成员名、非法类型和不可写出的非法 value，降低运行时排错成本。
- 保持 `DataTableProcessor` 编排路径稳定，通过已注册的 parser 和模板扩展实现。

## 推荐 Excel 格式

| Key | Type | Value | Comment |
| --- | --- | --- | --- |
| MaxLevel | int | 100 | 最大等级 |
| EnablePvp | bool | true | 是否开启 PVP |
| WelcomeText | string | 欢迎回来 | 登录欢迎文案 |
| DefaultRewards | array<int> | 1001,1002,1003 | 默认奖励 ID |
| FeatureWeights | map<string,int> | pve:10,pvp:5 | 功能权重 |
| ExtraConfig | json<GameConfig> | {"dailyLimit":3} | 扩展 JSON 配置 |

推荐约定：

- `Key` 必填、唯一，并且必须匹配 `^[A-Za-z][A-Za-z0-9_]*$`；当前不会把点号、短横线等字符转换为成员名。
- `Type` 必填，语法与普通字段类型保持一致。
- `Value` 必填；如果业务确实需要空值，应通过显式 nullable 或约定默认值表达。
- `Comment` 可选，只用于说明，不进入运行时 payload。

## Sheet 元信息

建议使用以下元信息声明：

```text
DTGen=kv,class=GameConfig,namespace=Game.DataTables
```

字段含义：

- `DTGen=kv`：选择 kv 表生成器。
- `class`：生成的配置类名；示例会生成 `DTGameConfig` 或项目配置的前缀形态。
- `namespace`：生成代码命名空间；如果省略，则使用生成器默认命名空间。

## 生成代码形态

当前实现会为每个 key 生成同名只读静态属性。示例：

```csharp
var maxLevel = DTGameConfig.MaxLevel;
var enablePvp = DTGameConfig.EnablePvp;
var rewards = DTGameConfig.DefaultRewards;
```

当前实现还会生成动态访问 API：

```csharp
var table = DataTableManager.GetDataTable<DTGameConfig>();
table?.TryGetValue("MaxLevel", out int? value);
table?.GetValue<int>("MaxLevel");
```

动态访问 API 只作为工具或兼容场景使用；业务代码应优先使用强类型属性。

## 类型与值解析

`kv` 复用现有字段类型解析和各类型 `DataProcessor`：

- 基础类型：`int`、`long`、`float`、`double`、`bool`、`string` 等。
- 枚举：按现有 enum 类型处理规则解析。
- 数组：例如 `array<int>`。
- 映射：例如 `map<string,int>`。
- JSON：例如 `json<GameConfig>`。
- 自定义类型：例如 `custom<MyStruct>`，由项目注册的处理器负责。

生成器会在解析阶段检查类型声明是否合法；具体 value 会作为内部单行数据在后续写出流程中按声明类型处理。

## 当前校验规则

### 必须报错

- `Key` 为空。
- `Key` 重复。
- `Key` 不匹配 `^[A-Za-z][A-Za-z0-9_]*$`。
- `Type` 为空或不是受支持类型。
- `Value` 不能按 `Type` 成功解析。

### 后续可增强

- 更细粒度地报告 JSON value 与声明目标类型不匹配。
- 支持项目自定义 key 命名风格警告。
- 支持显式别名或符号归一化策略。

## 诊断信息

当前重复 key 的错误消息会同时指出首次出现行和重复出现行。后续结构化诊断可进一步包含：

- severity
- file
- sheet
- row
- cell
- key
- field
- errorCode
- message


## 二进制 payload

当前实现把 kv 字段转换为内部单行数据，复用现有表写出流程。后续如需独立 kv payload，可采用确定性顺序写出：

1. 按 Excel 行顺序或按 key 排序写出条目；推荐默认保持 Excel 行顺序，便于诊断和 diff。
2. 每个条目写出 key、类型签名和 value payload。
3. 结构化 header 继续使用 v3 header、schema hash、generator version、table full name 和 flags。

如果后续引入独立 kv payload，可保留 key 到 value 的元数据，方便调试工具和兼容 API 查询。

## 与索引和预热的关系

- kv 表不需要普通行表索引；`Key` 本身就是唯一键。
- kv 表可参与显式表注册和 `PreheatAsync`，以便服务端或客户端启动时提前加载配置。
- 预热失败会暴露表加载失败；后续可增强为报告具体 key 和 value 解析错误。

## 后续实现建议

1. 新增更完整的 `KvTableValidator`，统一实现 value 解析诊断。
2. 如有需要，新增 kv serialization plan 或 writer，避免未来把特殊 payload 写出逻辑放进 `DataTableProcessor`。
3. 增加 kv 正常生成、重复 key、非法类型、非法 value、嵌套类型和 JSON 的测试。

## 非目标

- 不在第一版支持复杂表达式或公式语言。
- 不把 kv 表设计成可变运行时配置；运行时 API 默认只读。
- 不通过修改 `DataTableProcessor` 主流程来硬编码 kv 分支。
