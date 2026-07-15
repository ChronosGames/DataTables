# tree 表类型设计

`tree` 是已实现的树形配置表类型，适合表达有父子层级关系的数据，例如章节目录、任务链、技能树、菜单结构和组织结构。当前生成器会解析 `DTGen=tree`，复用普通行表数据格式，并额外生成树查询 API。

## 目标

- 用表格方式表达稳定的父子层级关系。
- 在生成期发现缺失必需字段、非法字段类型和普通行表可发现的数据错误；孤儿节点、循环引用和非法根节点校验可作为后续增强。
- 生成强类型查询 API，支持按节点、父节点、子节点和根节点查询。
- 支持客户端或服务端通过预热提前加载树表；完整结构校验可作为后续增强。
- 通过已注册的 parser 和模板实现，不在 `DataTableProcessor` 主流程中硬编码 tree 分支。

## 推荐 Excel 格式

| Id | ParentId | Order | Name | Type | Payload | Comment |
| --- | --- | --- | --- | --- | --- | --- |
| Root |  | 0 | 主线 | chapter | {} | 根节点 |
| Chapter01 | Root | 10 | 第一章 | chapter | {} | 章节节点 |
| Quest001 | Chapter01 | 10 | 初始任务 | quest | {"level":1} | 任务节点 |
| Quest002 | Chapter01 | 20 | 进阶任务 | quest | {"level":2} | 任务节点 |

推荐约定：

- `Id` 必填、唯一，并且作为树节点主键。
- `ParentId` 为空表示根节点；当前解析器会按 `ParentId` 建立分组索引，但不在解析阶段检查非空 `ParentId` 是否引用已存在的 `Id`。
- `Order` 可选；字段类型留空时会默认为 `int`。当前生成的 `GetChildren` 返回分组索引顺序，不额外按 `Order` 排序。
- `Name`、`Type`、`Payload` 为业务字段，可以按项目需要扩展。
- `Comment` 可选，只用于说明，不进入运行时 payload。

## Sheet 元信息

建议使用以下元信息声明：

```text
DTGen=tree,class=QuestTree,namespace=Game.DataTables
```

字段含义：

- `DTGen=tree`：选择 tree 表生成器。
- `class`：生成的树表类名。
- `namespace`：生成代码命名空间；省略时使用默认命名空间。

## 生成代码形态

当前实现生成节点类型和树表查询 API：

```csharp
var rootNodes = DTQuestTree.GetRoots(context);
var node = DTQuestTree.GetById(context, "Quest001");
var children = DTQuestTree.GetChildren(context, "Chapter01");
var parent = DTQuestTree.GetParent(context, "Quest001");
DTQuestTree.TryGetById(context, "Quest002", out var questNode);
```

当前实现也会生成深度优先遍历 API：

```csharp
foreach (var node in DTQuestTree.TraverseDepthFirst(context, "Root"))
{
    // visit node
}
```

## 当前校验规则

### 必须报错

- 缺少 `Id` 或 `ParentId` 字段。
- `Id` 或 `ParentId` 显式声明为非 `string` 类型。
- `Id` 重复会通过内建唯一索引写出/加载流程报错。
- `Order` 存在且值不能按 `int` 或显式声明类型解析。
- 业务字段不能按声明类型解析。

### 后续可增强

- 校验非空 `ParentId` 是否引用已存在的 `Id`。
- 校验循环引用和根节点数量。
- 对同一父节点下重复 `Order`、过深节点等输出项目级警告。

## 诊断信息

后续结构化诊断可进一步包含：

- severity
- file
- sheet
- row
- cell
- nodeId
- parentId
- field
- errorCode
- message

如果后续实现循环引用校验，诊断应输出完整路径，例如 `A -> B -> C -> A`，便于策划定位链路。

## 二进制 payload

当前实现复用普通行表 payload，并通过内建 `Id` 索引和 `ParentId` 分组索引支持树查询。后续如需独立树 payload，可写出两类数据：

1. 节点顺序表：按 Excel 行顺序或稳定排序写出完整节点数据。
2. 关系索引：写出 `Id -> index`、`ParentId -> child indexes` 和根节点索引列表。

如果后续写出独立关系索引，运行时加载后即可直接构建父子查询结构，避免每次查询都扫描全表。

## 与索引和预热的关系

- `Id` 是 tree 表的内建唯一索引。
- `ParentId` 是内建分组索引。
- tree 表可参与显式表注册和 `PreheatAsync`。
- 当前预热失败会暴露表加载失败；后续可增强为报告节点 id、父节点 id 和循环路径等上下文。

## 后续实现建议

1. 新增更完整的 `TreeTableValidator`，实现引用完整性、循环检测和项目级根节点规则。
2. 如有需要，新增 tree serialization plan 或 writer，写出节点数据和关系索引。
3. 增加正常树、多根树、孤儿节点、重复节点和循环引用测试。

## 非目标

- 第一版不支持运行时动态增删节点。
- 第一版不支持跨表父子引用；跨表关系应由业务层或后续 graph 表类型处理。
- 不通过修改 `DataTableProcessor` 主流程来硬编码 tree 分支。
