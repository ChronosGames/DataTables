# graph 表类型设计

`graph` 是计划中的图结构配置表类型，适合表达节点与边组成的非树形关系，例如关卡拓扑、科技依赖、任务依赖、传送网络和状态机。本文档仅描述原型设计；在实现完成前，当前生成器仍应对 `DTGen=graph` 给出“预留类型”诊断，而不是生成代码或数据。

## 目标

- 用可编辑表格表达节点和边的关系。
- 支持有向图和无向图的基础建模。
- 在生成期发现重复节点、非法边引用和不符合项目规则的环。
- 生成强类型查询 API，支持按节点查询入边、出边和邻接节点。
- 通过注册 parser、validator、writer 和模板实现，不在 `DataTableProcessor` 主流程中硬编码 graph 分支。

## 推荐 Excel 格式

graph 可以使用单 Sheet 双区域，也可以使用两个 Sheet。原型推荐两个 Sheet，便于策划维护。

### 节点 Sheet

| NodeId | NodeType | Name | Payload | Comment |
| --- | --- | --- | --- | --- |
| Start | state | 开始 | {} | 起点 |
| Battle01 | battle | 第一场战斗 | {"monster":1001} | 战斗节点 |
| Reward01 | reward | 奖励 | {"item":2001} | 奖励节点 |

### 边 Sheet

| EdgeId | From | To | EdgeType | Weight | Payload | Comment |
| --- | --- | --- | --- | --- | --- | --- |
| E001 | Start | Battle01 | next | 1 | {} | 起点到战斗 |
| E002 | Battle01 | Reward01 | reward | 1 | {} | 战斗到奖励 |
| E003 | Reward01 | Battle01 | retry | 5 | {} | 可选回退 |

推荐约定：

- `NodeId` 必填、唯一，并且作为节点主键。
- `EdgeId` 必填、唯一；如果项目不需要边主键，可以由生成器按行号生成稳定 id。
- `From` 和 `To` 必须引用存在的 `NodeId`。
- `EdgeType` 可用于区分依赖、跳转、奖励、解锁等关系。
- `Weight` 可选，用于寻路、排序或概率权重。
- `Payload` 可选，用于存储边或节点扩展数据。

## Sheet 元信息

建议使用以下元信息声明：

```text
DTGen=graph,class=LevelGraph,namespace=Game.DataTables
```

如果使用两个 Sheet，可以约定：

```text
DTGen=graph,nodes=LevelGraphNodes,edges=LevelGraphEdges,class=LevelGraph
```

字段含义：

- `DTGen=graph`：选择 graph 表生成器。
- `class`：生成的图表类名。
- `nodes`：节点 Sheet 名称。
- `edges`：边 Sheet 名称。
- `namespace`：生成代码命名空间；省略时使用默认命名空间。

## 生成代码形态

推荐生成节点、边和图查询 API：

```csharp
var node = DTLevelGraph.GetNode("Battle01");
var outgoing = DTLevelGraph.GetOutgoingEdges("Battle01");
var incoming = DTLevelGraph.GetIncomingEdges("Battle01");
var neighbors = DTLevelGraph.GetNeighbors("Battle01");
DTLevelGraph.TryGetEdge("E001", out var edge);
```

如果项目启用路径能力，可以额外生成工具 API：

```csharp
DTLevelGraph.HasPath("Start", "Reward01");
DTLevelGraph.FindPath("Start", "Reward01");
```

路径 API 是否生成应由配置控制，避免为所有项目引入额外运行时成本。

## 校验规则

### 必须报错

- `NodeId` 为空或重复。
- `EdgeId` 为空或重复，且未启用自动生成边 id。
- `From` 或 `To` 引用不存在的节点。
- `Weight` 不是合法数字。
- 项目声明为无环图时检测到环。
- 业务字段或 JSON payload 不能按声明类型解析。

### 可以警告

- 节点没有任何入边或出边。
- 存在从入口节点不可达的节点。
- 同一对节点之间存在多条同类型边。
- 权重为 0 或负数，但项目未禁止。

## 诊断要求

结构化诊断至少应包含：

- severity
- file
- sheet
- row
- cell
- nodeId
- edgeId
- from
- to
- field
- errorCode
- message

非法边引用应指出边所在行、边 id、缺失的端点和端点字段名。环检测应输出构成环的节点路径。

## 二进制 payload 建议

推荐写出以下结构：

1. 节点表：按稳定顺序写出节点数据。
2. 边表：按稳定顺序写出边数据。
3. 邻接索引：写出 `NodeId -> outgoing edge indexes` 和 `NodeId -> incoming edge indexes`。
4. 可选入口节点列表：用于可达性校验和运行时快速入口查询。

运行时加载后应避免临时扫描全边表来回答邻接查询。

## 与索引和预热的关系

- `NodeId` 和 `EdgeId` 是 graph 表的内建唯一索引。
- `From` 和 `To` 是内建分组索引。
- graph 表应参与显式表注册和 `PreheatAsync`。
- 预热失败应暴露节点 id、边 id、端点和环路径等上下文。

## 实现步骤建议

1. 新增 `GraphTableParser`，支持节点区和边区解析，或支持节点 Sheet + 边 Sheet 组合解析。
2. 新增 `GraphTableValidator`，实现唯一性、边引用、可达性和可选环检测。
3. 新增 graph serialization plan 或 writer，写出节点表、边表和邻接索引。
4. 新增 graph 代码生成模板，输出节点、边和图查询 API。
5. 为 `DTGen=graph` 注册 parser、validator、writer 和模板。
6. 增加正常图、重复节点、重复边、非法端点、不可达节点和环检测测试。

## 非目标

- 第一版不内置复杂图算法库；路径查询应作为可选能力。
- 第一版不支持运行时动态增删节点或边。
- 不通过修改 `DataTableProcessor` 主流程来硬编码 graph 分支。
