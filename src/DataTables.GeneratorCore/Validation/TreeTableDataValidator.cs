using System;
using System.Collections.Generic;
using System.Linq;

namespace DataTables.GeneratorCore;

internal sealed class TreeTableDataValidator : ITableDataValidator
{
    public string DataSetType => "tree";

    public void Validate(TableDataValidationContext context)
    {
        var idField = context.Schema.GetField("Id") ?? throw new InvalidOperationException("DTGen=tree 缺少 Id 字段");
        var parentField = context.Schema.GetField("ParentId") ?? throw new InvalidOperationException("DTGen=tree 缺少 ParentId 字段");
        var orderField = context.Schema.GetField("Order");
        var nodes = new Dictionary<string, (string ParentId, int Row)>(StringComparer.Ordinal);

        for (int i = context.FirstDataRowIndex; i <= context.Sheet.LastRowNum; i++)
        {
            var row = context.Sheet.GetRow(i);
            if (!context.ShouldValidateRow(row)) continue;
            var id = context.GetCellString(row!.GetCell(idField.Index));
            var parentId = context.GetCellString(row.GetCell(parentField.Index));
            if (string.IsNullOrWhiteSpace(id)) throw new FormatException($"DTGen=tree Id 为空: row={i + 1}, cell={TableDataValidationContext.GetCellReference(i, idField.Index)}");
            if (!nodes.TryAdd(id, (parentId, i))) throw new FormatException($"DTGen=tree Id 重复: nodeId={id}, row={i + 1}, cell={TableDataValidationContext.GetCellReference(i, idField.Index)}");
            if (orderField != null)
            {
                var orderText = context.GetCellString(row.GetCell(orderField.Index));
                if (!string.IsNullOrWhiteSpace(orderText) && !decimal.TryParse(orderText, out _))
                {
                    throw new FormatException($"DTGen=tree Order 不是合法数字: nodeId={id}, row={i + 1}, cell={TableDataValidationContext.GetCellReference(i, orderField.Index)}, value={orderText}");
                }
            }
        }

        foreach (var (id, node) in nodes)
        {
            if (!string.IsNullOrEmpty(node.ParentId) && !nodes.ContainsKey(node.ParentId))
            {
                throw new FormatException($"DTGen=tree ParentId 引用不存在: nodeId={id}, parentId={node.ParentId}, row={node.Row + 1}");
            }
        }

        var visited = new HashSet<string>(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var path = new List<string>();
        foreach (var id in nodes.Keys) Visit(id);

        void Visit(string id)
        {
            if (visited.Contains(id)) return;
            if (!visiting.Add(id))
            {
                var start = path.IndexOf(id);
                var cycle = start >= 0 ? path.Skip(start).Concat(new[] { id }) : new[] { id, id };
                throw new FormatException($"DTGen=tree 检测到循环引用: {string.Join(" -> ", cycle)}");
            }

            path.Add(id);
            var parentId = nodes[id].ParentId;
            if (!string.IsNullOrEmpty(parentId) && nodes.ContainsKey(parentId)) Visit(parentId);
            path.RemoveAt(path.Count - 1);
            visiting.Remove(id);
            visited.Add(id);
        }
    }
}
