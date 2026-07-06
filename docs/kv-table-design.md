# kv 表类型设计

`kv` 是计划中的键值配置表类型，面向全局参数、功能开关、少量散列配置和不适合拆成多行记录的小型配置集合。本文档仅描述原型设计；在实现完成前，当前生成器仍应对 `DTGen=kv` 给出“预留类型”诊断，而不是生成代码或数据。

## 目标

- 用统一格式表达全局参数和功能开关，避免为少量配置创建大量只有一行的数据表。
- 复用现有 DataTables 类型系统，支持基础类型、枚举、数组、JSON 和自定义类型。
- 生成强类型只读属性，减少业务代码中的字符串 key 查询。
- 在生成期发现重复 key、非法类型和非法 value，降低运行时排错成本。
- 保持 `DataTableProcessor` 编排路径稳定，通过注册新的 parser、validator、writer 和模板扩展实现。

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

- `Key` 必填、唯一，并且应是合法 C# 成员名；如果允许点号或短横线，生成器必须提供确定性的成员名转换规则。
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

推荐生成只读静态属性或只读实例属性，具体取决于项目现有表访问模式。示例：

```csharp
var maxLevel = DTGameConfig.MaxLevel;
var enablePvp = DTGameConfig.EnablePvp;
var rewards = DTGameConfig.DefaultRewards;
```

当需要保留动态访问能力时，可以额外生成：

```csharp
DTGameConfig.TryGetValue("MaxLevel", out var value);
DTGameConfig.GetValue<int>("MaxLevel");
```

动态访问 API 只作为工具或兼容场景使用；业务代码应优先使用强类型属性。

## 类型与值解析

`kv` 应复用现有 `DataTypeParser` 和各类型 `DataProcessor`：

- 基础类型：`int`、`long`、`float`、`double`、`bool`、`string` 等。
- 枚举：按现有 enum 类型处理规则解析。
- 数组：例如 `array<int>`。
- 映射：例如 `map<string,int>`。
- JSON：例如 `json<GameConfig>`。
- 自定义类型：例如 `custom<MyStruct>`，由项目注册的处理器负责。

生成器应在写出 `.bytes` 前完成 value 解析和校验，避免运行时首次加载时才发现格式错误。

## 校验规则

### 必须报错

- `Key` 为空。
- `Key` 重复。
- `Key` 无法转换为合法生成成员名，且没有配置显式别名。
- `Type` 为空或不是受支持类型。
- `Value` 不能按 `Type` 成功解析。
- JSON value 不是合法 JSON，或不能映射到声明的 JSON 目标类型。

### 可以警告

- `Key` 命名不符合项目推荐风格。
- `Comment` 为空。
- `Value` 使用了兼容模式才允许的旧格式。
- 动态访问 key 与生成属性名发生大小写或符号归一化冲突。

## 诊断要求

诊断应使用结构化格式，并至少包含：

- severity
- file
- sheet
- row
- cell
- key
- field
- errorCode
- message

重复 key 的错误消息应同时指出首次出现行和重复出现行，便于策划直接定位 Excel。

## 二进制 payload 建议

kv payload 可以采用确定性顺序写出：

1. 按 Excel 行顺序或按 key 排序写出条目；推荐默认保持 Excel 行顺序，便于诊断和 diff。
2. 每个条目写出 key、类型签名和 value payload。
3. 结构化 header 继续使用 v3 header、schema hash、generator version、table full name 和 flags。

如果最终生成代码只使用强类型属性，运行时也应保留 key 到 value 的元数据，方便调试工具和兼容 API 查询。

## 与索引和预热的关系

- kv 表不需要普通行表索引；`Key` 本身就是唯一键。
- kv 表应参与显式表注册和 `PreheatAsync`，以便服务端或客户端启动时提前校验配置。
- 预热失败应暴露具体 key 和 value 解析错误，而不是只报告表加载失败。

## 实现步骤建议

1. 新增 `KvTableParser`，从固定列 `Key`、`Type`、`Value`、`Comment` 解析 kv schema。
2. 新增 `KvTableValidator`，实现 key 唯一性、成员名、类型和值校验。
3. 新增 kv serialization plan 或 writer，避免把 kv payload 写出逻辑放进 `DataTableProcessor`。
4. 新增 kv 代码生成模板，输出强类型属性和可选动态访问 API。
5. 为 `DTGen=kv` 注册 parser、validator、writer 和模板。
6. 增加 kv 正常生成、重复 key、非法类型、非法 value、嵌套类型和 JSON 的测试。

## 非目标

- 不在第一版支持复杂表达式或公式语言。
- 不把 kv 表设计成可变运行时配置；运行时 API 默认只读。
- 不通过修改 `DataTableProcessor` 主流程来硬编码 kv 分支。
