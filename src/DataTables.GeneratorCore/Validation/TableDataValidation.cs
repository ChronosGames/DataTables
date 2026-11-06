using System;
using System.Collections.Generic;
using System.Linq;

namespace DataTables.GeneratorCore;

internal enum TableDataValidationPhase
{
    CellConversion = 0,
    RowRules = 100,
    RelationshipRules = 200,
    ProjectRules = 300,
}

internal sealed class TableDataValidationContext
{
    public TableDataValidationContext(
        GenerationContext generationContext,
        TableDataSnapshot snapshot,
        DiagnosticsCollector diagnostics)
    {
        GenerationContext = generationContext;
        Snapshot = snapshot;
        Diagnostics = diagnostics;
    }

    public GenerationContext GenerationContext { get; }

    public TableDataSnapshot Snapshot { get; }

    public DiagnosticsCollector Diagnostics { get; }

    public void Error(TableDataCell cell, string message)
    {
        Diagnostics.Error(
            GenerationContext.FileName,
            GenerationContext.SheetName,
            cell.CellReference,
            message);
    }
}

internal interface ITableDataValidationPass
{
    TableDataValidationPhase Phase { get; }

    void Validate(TableDataValidationContext context);
}

internal sealed class CellConversionValidationPass : ITableDataValidationPass
{
    public TableDataValidationPhase Phase => TableDataValidationPhase.CellConversion;

    public void Validate(TableDataValidationContext context)
    {
        foreach (var cell in context.Snapshot.Cells.Where(cell => cell.State == TableDataCellState.Invalid))
        {
            var location = string.IsNullOrEmpty(cell.CellReference) ? string.Empty : $" ({cell.CellReference})";
            if (cell.Error is DataTableProcessor.DataTypeParseException)
            {
                context.Error(
                    cell,
                    $"字段 '{cell.FieldName}' 类型 '{cell.TypeName}' 解析失败{location}: {GetRootMessage(cell.Error)}");
                continue;
            }

            context.Error(
                cell,
                $"字段 '{cell.FieldName}' 的值无法按类型 '{cell.TypeName}' 解析{location}: '{cell.RawValue}'. {GetRootMessage(cell.Error)}");
        }
    }

    private static string GetRootMessage(Exception? exception)
    {
        if (exception == null)
        {
            return "未知转换错误。";
        }

        while (exception.InnerException != null)
        {
            exception = exception.InnerException;
        }

        return exception.Message;
    }
}
