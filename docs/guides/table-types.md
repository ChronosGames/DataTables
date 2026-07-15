# DataTables 表类型

本文档说明当前已支持和计划中的表格布局。表类型由 Sheet 元信息行中的 `DTGen` 值选择。

## 已支持类型

当前代码中的默认解析器注册了 `table`、`matrix`、`column`、`kv`、`tree` 和 `graph`；代码模板也为这些类型提供生成器。

所有类型的生成静态查询都是 context-first。默认表把 `DataTableContext` 放在第一参数；命名表把名称放在第二参数：

```csharp
var item = DTItem.GetById(context, 1001);
var shardItem = DTItem.GetById(context, "x001", 1001);
```

生成查询不会访问默认 `DataTableManager`。无 context 的静态查询、静态查询属性和 `*Static` 兼容别名已删除，实例查询 API 保留。Matrix 使用 `GetTable(context[, name])`、`GetTableOrNull(context[, name])`、`IsLoaded(context[, name])`；KV 强类型值使用 `GetXxx(context[, name])`。

## 表级标签

A1 元信息可声明工作表或子逻辑表标签：

```text
DTGen=table,class=Item,tags=S&C
```

`-t` / `FilterColumnTags` 仍是布尔表达式，支持 `NOT`/`!`、`AND`/`&&`、`OR`/`||` 和括号。未声明 `tags=` 的表始终包含；每个 Sheet/child 独立判断。`disabletagsfilter` 会同时关闭表级和字段 `@tag` 过滤。非法 `tags=` 定位到 A1，非法全局表达式在普通导出与 `validate` 中都会产生 Error diagnostic。

### `table`

面向行的数据表。每一行数据都会生成一个行对象，并且可以参与索引构建。

推荐场景：

- 道具表
- 怪物表
- 等级曲线
- 客户端与服务端共享配置

### `matrix`

面向矩阵的数据表。行坐标和列坐标都具备业务含义。

推荐场景：

- 二维数值平衡表
- 关系矩阵
- 由两个维度共同定位的消耗表

### `column`

面向列的数据表。适合把一个逻辑记录纵向存放，而不是横向存放。

推荐场景：

- 更便于编辑器填写的小型配置表
- 字段较多、说明列较多或本地化列较多的小表

### `kv`

`kv` 已支持用于全局参数、功能开关和少量散列配置。完整说明见 [kv 表类型设计](../designs/kv-table-design.md)。当前实现要求 `Key`、`Type`、`Value` 三列，并可选 `Comment` 列；每条配置会生成一行内部数据，生成表类会暴露 context-first 强类型方法以及实例动态读取 API。推荐 Excel 结构如下：

| Key | Type | Value | Comment |
| --- | --- | --- | --- |
| MaxLevel | int | 100 | 最大等级 |
| EnablePvp | bool | true | 是否开启 PVP |
| DropRates | array<int> | 1,2,3 | 示例数组值 |
| ExtraJson | json<GameConfig> | {"foo":1} | JSON 载荷 |

已生成的强类型 API 形态：

```csharp
DTGameConfig.GetMaxLevel(context)
DTGameConfig.GetEnablePvp(context, "regional")
context.GetCached<DTGameConfig>()?.GetValue<int>("MaxLevel")
context.GetCached<DTGameConfig>()?.TryGetValue("EnablePvp", out bool? enablePvp)
```

当前校验与生成规则：

- `Key` 必须唯一，并且必须匹配合法 C# 成员名格式 `^[A-Za-z][A-Za-z0-9_]*$`。
- `Type` 必填，并会复用普通字段的 DataTables 类型解析器做类型合法性检查。
- `Value` 必填，并作为生成出的内部单行数据参与后续写出。
- `Comment` 可选；存在时用作生成属性的 XML 摘要。

### `tree`

`tree` 已支持用于树形层级配置，例如章节、任务链、技能树和菜单结构。完整说明见 [tree 表类型设计](../designs/tree-table-design.md)。当前实现复用普通行表格式，要求 `Id` 和 `ParentId` 字段，并会内建 `Id` 唯一索引以及 `ParentId` 分组索引；生成表类会提供根节点、子节点、父节点和深度优先遍历 API。

### `graph`

`graph` 已支持单 Sheet 边列表格式，用于节点与边组成的图结构配置，例如关卡拓扑、科技依赖、传送网络和状态机。必需字段为 `EdgeId`、`From` 和 `To`，生成器会内建 `EdgeId` 唯一索引以及 `From` / `To` 分组索引，并生成节点集合、边查询、入边/出边、关联边、前驱/后继、两点边、路径、BFS 遍历和节点存在性查询 API。完整说明见 [graph 表类型设计](../designs/graph-table-design.md)。

## 预留原型类型

解析器注册表会有意预留部分类型名称。这样项目使用尚未实现的表类型时，可以得到明确诊断，而不是静默回退到 `table` 语义。

### `localized`

`localized` 计划用于多语言文本资源，目前还不是已实现的表生成器。详细设计见 [localized 表类型设计](../designs/localized-table-design.md)。

### 其他预留名称

`partitioned`、`versioned` 和 `patch` 预留给未来的运行时加载、热更新和补丁场景；它们目前还不是已实现的表生成器。
