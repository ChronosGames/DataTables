# DataTables 表类型

本文档说明当前已支持和计划中的表格布局。表类型由 Sheet 元信息行中的 `DTGen` 值选择。

## 已支持类型

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

## 预留原型类型

解析器注册表会有意预留部分类型名称。这样项目使用尚未实现的表类型时，可以得到明确诊断，而不是静默回退到 `table` 语义。

### `kv`

`kv` 计划用于全局参数、功能开关和少量散列配置。完整原型设计见 [kv 表类型设计](kv-table-design.md)。推荐 Excel 结构如下：

| Key | Type | Value | Comment |
| --- | --- | --- | --- |
| MaxLevel | int | 100 | 最大等级 |
| EnablePvp | bool | true | 是否开启 PVP |
| DropRates | array<int> | 1,2,3 | 示例数组值 |
| ExtraJson | json<GameConfig> | {"foo":1} | JSON 载荷 |

计划生成的强类型 API 形态：

```csharp
DTGameConfig.MaxLevel
DTGameConfig.EnablePvp
```

计划校验规则：

- `Key` 必须唯一，并且必须是合法的生成成员名。
- `Type` 复用普通字段的 DataTables 类型解析器。
- `Value` 在写出 `.bytes` 前必须使用所选类型处理器完成校验。
- 严格模式下，重复 key 和非法 value 应报告为生成错误；兼容模式可以把部分发现降级为警告。

### `localized`

`localized` 计划用于多语言文本资源。详细设计见 [localized 表类型设计](localized-table-design.md)。

### `tree`

`tree` 计划用于树形层级配置，例如章节、任务链、技能树和菜单结构。完整原型设计见 [tree 表类型设计](tree-table-design.md)。

### `graph`

`graph` 已支持单 Sheet 边列表格式，用于节点与边组成的图结构配置，例如关卡拓扑、科技依赖、传送网络和状态机。必需字段为 `EdgeId`、`From` 和 `To`，生成器会内建 `EdgeId` 唯一索引以及 `From` / `To` 分组索引，并生成 `GetEdge`、`TryGetEdge`、`GetOutgoingEdges`、`GetIncomingEdges`、`GetNeighbors` 和节点存在性查询 API。完整说明见 [graph 表类型设计](graph-table-design.md)。

### 其他预留名称

`partitioned`、`versioned` 和 `patch` 预留给未来的运行时加载、热更新和补丁场景；它们目前还不是已实现的表生成器。
