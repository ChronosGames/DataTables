# tree 表类型设计

`tree` 是计划中的树形配置表类型，适合表达有父子层级关系的数据，例如章节目录、任务链、技能树、菜单结构和组织结构。本文档仅描述原型设计；在实现完成前，当前生成器仍应对 `DTGen=tree` 给出“预留类型”诊断，而不是生成代码或数据。

## 目标

- 用表格方式表达稳定的父子层级关系。
- 在生成期发现孤儿节点、重复节点、循环引用和非法根节点。
- 生成强类型查询 API，支持按节点、父节点、子节点和根节点查询。
- 支持客户端或服务端预热时提前校验完整树结构。
- 通过注册 parser、validator、writer 和模板实现，不在 `DataTableProcessor` 主流程中硬编码 tree 分支。

## 推荐 Excel 格式

| Id | ParentId | Order | Name | Type | Payload | Comment |
| --- | --- | --- | --- | --- | --- | --- |
| Root |  | 0 | 主线 | chapter | {} | 根节点 |
| Chapter01 | Root | 10 | 第一章 | chapter | {} | 章节节点 |
| Quest001 | Chapter01 | 10 | 初始任务 | quest | {"level":1} | 任务节点 |
| Quest002 | Chapter01 | 20 | 进阶任务 | quest | {"level":2} | 任务节点 |

推荐约定：

- `Id` 必填、唯一，并且作为树节点主键。
- `ParentId` 为空表示根节点；非空时必须引用已存在的 `Id`。
- `Order` 可选，用于同级节点排序；为空时按 Excel 行顺序排序。
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

推荐生成节点类型和树表查询 API：

```csharp
var rootNodes = DTQuestTree.Roots;
var node = DTQuestTree.GetById("Quest001");
var children = DTQuestTree.GetChildren("Chapter01");
var parent = DTQuestTree.GetParent("Quest001");
DTQuestTree.TryGetById("Quest002", out var questNode);
```

如果业务需要遍历，可以额外生成：

```csharp
foreach (var node in DTQuestTree.TraverseDepthFirst("Root"))
{
    // visit node
}
```

## 校验规则

### 必须报错

- `Id` 为空或重复。
- `ParentId` 引用不存在的节点。
- 任意节点形成循环引用。
- 根节点数量不符合 Sheet 或项目配置要求。
- `Order` 不是合法数字。
- 业务字段不能按声明类型解析。

### 可以警告

- 同一父节点下多个子节点 `Order` 相同。
- 存在未被业务入口引用的孤立根节点，但配置允许多根。
- 节点深度超过项目推荐上限。

## 诊断要求

结构化诊断至少应包含：

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

循环引用诊断应输出完整路径，例如 `A -> B -> C -> A`，便于策划定位链路。

## 二进制 payload 建议

推荐写出两类数据：

1. 节点顺序表：按 Excel 行顺序或稳定排序写出完整节点数据。
2. 关系索引：写出 `Id -> index`、`ParentId -> child indexes` 和根节点索引列表。

运行时加载时应能直接构建父子查询结构，避免每次查询都扫描全表。

## 与索引和预热的关系

- `Id` 是 tree 表的内建唯一索引。
- `ParentId` 是内建分组索引。
- tree 表应参与显式表注册和 `PreheatAsync`。
- 预热失败应暴露节点 id、父节点 id 和循环路径等上下文。

## 实现步骤建议

1. 新增 `TreeTableParser`，识别固定列 `Id`、`ParentId`、`Order` 和业务字段。
2. 新增 `TreeTableValidator`，实现唯一性、引用完整性和循环检测。
3. 新增 tree serialization plan 或 writer，写出节点数据和关系索引。
4. 新增 tree 代码生成模板，输出节点类型和树查询 API。
5. 为 `DTGen=tree` 注册 parser、validator、writer 和模板。
6. 增加正常树、多根树、孤儿节点、重复节点和循环引用测试。

## 非目标

- 第一版不支持运行时动态增删节点。
- 第一版不支持跨表父子引用；跨表关系应由业务层或后续 graph 表类型处理。
- 不通过修改 `DataTableProcessor` 主流程来硬编码 tree 分支。
