using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DataTables.GeneratorCore;

internal sealed class TreeRowValidationPass : ITableDataValidationPass
{
    public TableDataValidationPhase Phase => TableDataValidationPhase.RowRules;

    public void Validate(TableDataValidationContext context)
    {
        var nodeIds = new Dictionary<string, TableDataCell>(StringComparer.Ordinal);
        foreach (var row in context.Snapshot.Rows)
        {
            var idCell = row.GetCell("Id") ?? throw new InvalidOperationException("DTGen=tree 缺少 Id 字段");
            var id = idCell.RawValue;
            if (string.IsNullOrWhiteSpace(id))
            {
                context.Error(idCell, $"DTGen=tree Id 为空: row={idCell.RowIndex + 1}");
                continue;
            }

            if (!nodeIds.TryAdd(id, idCell))
            {
                var first = nodeIds[id];
                context.Error(idCell, $"DTGen=tree Id 重复: nodeId={id}, firstCell={first.CellReference}");
            }

            if (row.GetCell("Order") is { } orderCell
                && !string.IsNullOrWhiteSpace(orderCell.RawValue)
                && !decimal.TryParse(orderCell.RawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
            {
                context.Error(orderCell, $"DTGen=tree Order 不是合法数字: nodeId={id}, value={orderCell.RawValue}");
            }
        }
    }
}

internal sealed class TreeRelationshipValidationPass : ITableDataValidationPass
{
    public TableDataValidationPhase Phase => TableDataValidationPhase.RelationshipRules;

    public void Validate(TableDataValidationContext context)
    {
        var nodes = context.Snapshot.Rows.ToDictionary(
            row => row.GetCell("Id")!.RawValue,
            row => new TreeNode(
                row.GetCell("Id")!,
                row.GetCell("ParentId") ?? throw new InvalidOperationException("DTGen=tree 缺少 ParentId 字段")),
            StringComparer.Ordinal);

        var errorCountBeforeReferences = context.Diagnostics.ErrorCount;
        foreach (var node in nodes.Values)
        {
            if (!string.IsNullOrEmpty(node.ParentCell.RawValue)
                && !nodes.ContainsKey(node.ParentCell.RawValue))
            {
                context.Error(
                    node.ParentCell,
                    $"DTGen=tree ParentId 引用不存在: nodeId={node.IdCell.RawValue}, parentId={node.ParentCell.RawValue}");
            }
        }

        if (context.Diagnostics.ErrorCount > errorCountBeforeReferences)
        {
            return;
        }

        var visited = new HashSet<string>(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var path = new List<string>();
        foreach (var id in nodes.Keys)
        {
            if (Visit(id))
            {
                return;
            }
        }

        bool Visit(string id)
        {
            if (visited.Contains(id))
            {
                return false;
            }

            if (!visiting.Add(id))
            {
                var start = path.IndexOf(id);
                var cycle = start >= 0 ? path.Skip(start).Concat([id]) : [id, id];
                context.Error(nodes[id].IdCell, $"DTGen=tree 检测到循环引用: {string.Join(" -> ", cycle)}");
                return true;
            }

            path.Add(id);
            var parentId = nodes[id].ParentCell.RawValue;
            if (!string.IsNullOrEmpty(parentId) && Visit(parentId))
            {
                return true;
            }

            path.RemoveAt(path.Count - 1);
            visiting.Remove(id);
            visited.Add(id);
            return false;
        }
    }

    private sealed record TreeNode(TableDataCell IdCell, TableDataCell ParentCell);
}
