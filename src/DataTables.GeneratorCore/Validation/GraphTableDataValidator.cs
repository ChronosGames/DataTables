using System;
using System.Collections.Generic;

namespace DataTables.GeneratorCore;

internal sealed class GraphTableDataValidator : ITableDataValidator
{
    public string DataSetType => "graph";

    public void Validate(TableDataValidationContext context)
    {
        var edgeIdField = context.Schema.GetField("EdgeId") ?? throw new InvalidOperationException("DTGen=graph 缺少 EdgeId 字段");
        var fromField = context.Schema.GetField("From") ?? throw new InvalidOperationException("DTGen=graph 缺少 From 字段");
        var toField = context.Schema.GetField("To") ?? throw new InvalidOperationException("DTGen=graph 缺少 To 字段");
        var weightField = context.Schema.GetField("Weight");
        var edgeIds = new HashSet<string>(StringComparer.Ordinal);

        for (int i = context.FirstDataRowIndex; i <= context.Sheet.LastRowNum; i++)
        {
            var row = context.Sheet.GetRow(i);
            if (!context.ShouldValidateRow(row)) continue;
            var edgeId = context.GetCellString(row!.GetCell(edgeIdField.Index));
            var from = context.GetCellString(row.GetCell(fromField.Index));
            var to = context.GetCellString(row.GetCell(toField.Index));
            if (string.IsNullOrWhiteSpace(edgeId)) throw new FormatException($"DTGen=graph EdgeId 为空: row={i + 1}, cell={TableDataValidationContext.GetCellReference(i, edgeIdField.Index)}");
            if (!edgeIds.Add(edgeId)) throw new FormatException($"DTGen=graph EdgeId 重复: edgeId={edgeId}, row={i + 1}, cell={TableDataValidationContext.GetCellReference(i, edgeIdField.Index)}");
            if (string.IsNullOrWhiteSpace(from)) throw new FormatException($"DTGen=graph From 为空: edgeId={edgeId}, row={i + 1}, cell={TableDataValidationContext.GetCellReference(i, fromField.Index)}");
            if (string.IsNullOrWhiteSpace(to)) throw new FormatException($"DTGen=graph To 为空: edgeId={edgeId}, row={i + 1}, cell={TableDataValidationContext.GetCellReference(i, toField.Index)}");
            if (weightField != null)
            {
                var weightText = context.GetCellString(row.GetCell(weightField.Index));
                if (!string.IsNullOrWhiteSpace(weightText) && !decimal.TryParse(weightText, out _))
                {
                    throw new FormatException($"DTGen=graph Weight 不是合法数字: edgeId={edgeId}, row={i + 1}, cell={TableDataValidationContext.GetCellReference(i, weightField.Index)}, value={weightText}");
                }
            }
        }
    }
}
