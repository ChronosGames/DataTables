using System;
using System.Collections.Generic;
using System.Globalization;

namespace DataTables.GeneratorCore;

internal sealed class GraphRowValidationPass : ITableDataValidationPass
{
    public TableDataValidationPhase Phase => TableDataValidationPhase.RowRules;

    public void Validate(TableDataValidationContext context)
    {
        var edgeIds = new Dictionary<string, TableDataCell>(StringComparer.Ordinal);
        foreach (var row in context.Snapshot.Rows)
        {
            var edgeIdCell = row.GetCell("EdgeId") ?? throw new InvalidOperationException("DTGen=graph 缺少 EdgeId 字段");
            var fromCell = row.GetCell("From") ?? throw new InvalidOperationException("DTGen=graph 缺少 From 字段");
            var toCell = row.GetCell("To") ?? throw new InvalidOperationException("DTGen=graph 缺少 To 字段");
            var edgeId = edgeIdCell.RawValue;

            if (string.IsNullOrWhiteSpace(edgeId))
            {
                context.Error(edgeIdCell, $"DTGen=graph EdgeId 为空: row={edgeIdCell.RowIndex + 1}");
            }
            else if (!edgeIds.TryAdd(edgeId, edgeIdCell))
            {
                context.Error(edgeIdCell, $"DTGen=graph EdgeId 重复: edgeId={edgeId}, firstCell={edgeIds[edgeId].CellReference}");
            }

            if (string.IsNullOrWhiteSpace(fromCell.RawValue))
            {
                context.Error(fromCell, $"DTGen=graph From 为空: edgeId={edgeId}");
            }

            if (string.IsNullOrWhiteSpace(toCell.RawValue))
            {
                context.Error(toCell, $"DTGen=graph To 为空: edgeId={edgeId}");
            }

            if (row.GetCell("Weight") is { } weightCell
                && !string.IsNullOrWhiteSpace(weightCell.RawValue)
                && !decimal.TryParse(weightCell.RawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
            {
                context.Error(weightCell, $"DTGen=graph Weight 不是合法数字: edgeId={edgeId}, value={weightCell.RawValue}");
            }
        }
    }
}
