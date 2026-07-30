using System;
using System.Collections.Generic;
using System.Linq;

namespace DataTables.GeneratorCore;

internal sealed class RowTableDataValidator : ITableDataValidator
{
    public string DataSetType => "table";

    public void Validate(TableDataValidationContext context)
    {
        foreach (var constraint in context.Schema.UniqueConstraints)
        {
            ValidateUniqueConstraint(context, constraint.Fields);
        }
    }

    private static void ValidateUniqueConstraint(TableDataValidationContext context, IReadOnlyList<string> fieldNames)
    {
        var fields = fieldNames.Select(name =>
            context.Schema.GetField(name) ?? throw new InvalidOperationException($"唯一索引引用了不存在的字段: {name}"))
            .ToArray();
        var firstRows = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var rowIndex = context.FirstDataRowIndex; rowIndex <= context.Sheet.LastRowNum; rowIndex++)
        {
            var row = context.Sheet.GetRow(rowIndex);
            if (!context.ShouldValidateRow(row)) continue;

            var values = fields.Select(field => context.GetCellString(row!.GetCell(field.Index))).ToArray();
            var key = string.Join("\u001F", values.Select(value => $"{value.Length}:{value}"));
            if (firstRows.TryGetValue(key, out var firstRow))
            {
                throw new FormatException(
                    $"唯一索引值重复: fields={string.Join("&", fieldNames)}, value=({string.Join(", ", values)}), " +
                    $"firstRow={firstRow + 1}, duplicateRow={rowIndex + 1}, " +
                    $"cell={TableDataValidationContext.GetCellReference(rowIndex, fields[0].Index)}");
            }

            firstRows.Add(key, rowIndex);
        }
    }
}
