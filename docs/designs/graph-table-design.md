# graph 表类型设计

`graph` 是已实现的图结构配置表类型，适合表达节点与边组成的非树形关系，例如关卡拓扑、科技依赖、任务依赖、传送网络和状态机。当前首版实现采用单 Sheet 边列表格式：每一行是一条边，节点集合由 `From` / `To` 端点自动推导。

## 目标

- 用可编辑表格表达节点和边的关系。
- 支持有向图和无向图的基础建模。
- 在生成期发现重复节点、非法边引用和不符合项目规则的环。
- 生成强类型图查询 API，支持节点集合、入边、出边、关联边、前驱、后继、两点边、路径和遍历。
- 通过注册 parser、validator、writer 和模板实现，不在 `DataTableProcessor` 主流程中硬编码 graph 分支。

## 推荐 Excel 格式

当前实现使用单 Sheet 边列表。后续版本仍可扩展为节点 Sheet + 边 Sheet 的组合格式。

### graph Sheet

| EdgeId | From | To | EdgeType | Weight | Payload | Comment |
| --- | --- | --- | --- | --- | --- | --- |
| E001 | Start | Battle01 | next | 1 | {} | 起点到战斗 |
| E002 | Battle01 | Reward01 | reward | 1 | {} | 战斗到奖励 |
| E003 | Reward01 | Battle01 | retry | 5 | {} | 可选回退 |

推荐约定：

- `EdgeId` 必填、唯一，并且作为边主键。
- `From` / `To` 必填；节点 id 由所有端点自动推导。
- `EdgeType` 可用于区分依赖、跳转、奖励、解锁等关系。
- `Weight` 可选，用于寻路、排序或概率权重。
- `Payload` 可选，用于存储边或节点扩展数据。

## Sheet 元信息

建议使用以下元信息声明：

```text
DTGen=graph,class=LevelGraph,namespace=Game.DataTables
```

字段含义：

- `DTGen=graph`：选择 graph 表生成器。
- `class`：生成的图表类名。
- `namespace`：生成代码命名空间；省略时使用默认命名空间。

## 生成代码形态

推荐生成节点、边和图查询 API：

```csharp
var nodes = DTLevelGraph.GetNodes(context);
var nodeId = DTLevelGraph.GetNode(context, "Battle01");
var outgoing = DTLevelGraph.GetOutgoingEdges(context, "Battle01");
var incoming = DTLevelGraph.GetIncomingEdges(context, "Battle01");
var incident = DTLevelGraph.GetIncidentEdges(context, "Battle01");
var successors = DTLevelGraph.GetSuccessors(context, "Battle01");
var predecessors = DTLevelGraph.GetPredecessors(context, "Battle01");
var neighbors = DTLevelGraph.GetNeighbors(context, "Battle01");
var edges = DTLevelGraph.GetEdgesBetween(context, "Start", "Battle01");
var hasEdge = DTLevelGraph.HasEdge(context, "Start", "Battle01");
var hasPath = DTLevelGraph.HasPath(context, "Start", "Reward01");
var path = DTLevelGraph.FindPath(context, "Start", "Reward01");
var bfs = DTLevelGraph.TraverseBreadthFirst(context, "Start");
DTLevelGraph.TryGetEdge(context, "E001", out var edge);
```

路径 API 使用广度优先搜索返回节点 id 路径；如果起点或终点不存在，或没有可达路径，则返回空列表。

## 校验规则

### 必须报错

- `EdgeId` 为空或重复。
- `From` 或 `To` 为空。
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

- `EdgeId` 是 graph 表的内建唯一索引。
- `From` 和 `To` 是内建分组索引，用于生成入边、出边、关联边、前驱、后继、两点边、路径和遍历查询。
- graph 表应参与显式表注册和 `PreheatAsync`。
- 预热失败应暴露节点 id、边 id、端点和环路径等上下文。

## 实现步骤建议

1. 已新增 `GraphTableParser`，基于普通行表解析边列表并注册内建索引。
2. 已新增生成期行校验，检查 `EdgeId`、`From`、`To` 和可选 `Weight`。
3. 已复用普通表二进制写出流程，运行时通过内建索引构建邻接查询。
4. 已新增 graph 代码生成模板，输出节点集合、边、邻接、前驱/后继、两点边、路径和 BFS 遍历 API。
5. 已为 `DTGen=graph` 注册 parser 和模板。
6. 已增加 parser 与模板测试；后续可继续补充节点 Sheet、环检测和路径 API 测试。

## 非目标

- 第一版不内置复杂图算法库；路径查询应作为可选能力。
- 第一版不支持运行时动态增删节点或边。
- 不通过修改 `DataTableProcessor` 主流程来硬编码 graph 分支。
