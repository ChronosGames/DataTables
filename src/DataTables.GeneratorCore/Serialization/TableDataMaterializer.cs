using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NPOI.SS.UserModel;

namespace DataTables.GeneratorCore;

internal enum TableDataLayout
{
    Row,
    Matrix,
    Column,
    KeyValue,
}

internal sealed class TableDataMaterializationContext
{
    public TableDataMaterializationContext(
        GenerationContext generationContext,
        ISheet sheet,
        int firstDataRowIndex,
        DiagnosticsCollector diagnostics,
        Func<ICell?, string> getCellString)
    {
        GenerationContext = generationContext;
        Sheet = sheet;
        FirstDataRowIndex = firstDataRowIndex;
        Diagnostics = diagnostics;
        GetCellString = getCellString;
    }

    public GenerationContext GenerationContext { get; }

    public ISheet Sheet { get; }

    public int FirstDataRowIndex { get; }

    public DiagnosticsCollector Diagnostics { get; }

    public Func<ICell?, string> GetCellString { get; }
}

internal interface ITableDataMaterializer
{
    TableDataSnapshot Materialize(TableDataMaterializationContext context);
}

internal sealed class TableDataMaterializer : ITableDataMaterializer
{
    private static readonly Regex s_CellRowRegex = new(@"\d+", RegexOptions.Compiled);

    private readonly TableDataLayout m_Layout;

    public TableDataMaterializer(TableDataLayout layout)
    {
        m_Layout = layout;
    }

    public TableDataSnapshot Materialize(TableDataMaterializationContext context)
    {
        var fieldEncoders = new Dictionary<XField, FieldEncoder>();
        var invalidTypeCells = context.GenerationContext.Fields
            .Where(field => !field.IsIgnore)
            .Select(field => (Field: field, Encoder: GetFieldEncoder(field, fieldEncoders)))
            .Where(item => item.Encoder.Error != null)
            .Select(item => new TableDataCell(
                item.Field,
                TryGetRowIndex(item.Field.TypeCell),
                TryGetColumnIndex(item.Field.TypeCell),
                item.Field.TypeCell,
                string.Empty,
                TableDataCellState.Invalid,
                [],
                item.Encoder.Error))
            .ToArray();
        if (invalidTypeCells.Length > 0)
        {
            return new TableDataSnapshot([new TableDataRow(sourceRowIndex: -1, invalidTypeCells)]);
        }

        return m_Layout switch
        {
            TableDataLayout.Row => MaterializeRows(context, fieldEncoders),
            TableDataLayout.Matrix => MaterializeMatrix(context, fieldEncoders),
            TableDataLayout.Column => MaterializeColumns(context, fieldEncoders),
            TableDataLayout.KeyValue => MaterializeKeyValue(context, fieldEncoders),
            _ => throw new ArgumentOutOfRangeException(nameof(m_Layout), m_Layout, "Unknown table data layout."),
        };
    }

    private TableDataSnapshot MaterializeRows(TableDataMaterializationContext context, Dictionary<XField, FieldEncoder> fieldEncoders)
    {
        var rows = new List<TableDataRow>();
        for (int rowIndex = context.FirstDataRowIndex; rowIndex <= context.Sheet.LastRowNum; rowIndex++)
        {
            var row = context.Sheet.GetRow(rowIndex);
            if (row == null || row.FirstCellNum < 0)
            {
                continue;
            }

            var reader = new RawRowReader(row, context.GetCellString);
            var commentReadErrors = context.GenerationContext.Fields
                .Where(field => field.IsComment && reader.Read(field.Index).Error != null)
                .ToArray();
            if (commentReadErrors.Length > 0)
            {
                rows.Add(new TableDataRow(
                    rowIndex,
                    commentReadErrors.Select(field => EncodeCell(field, rowIndex, field.Index, reader.Read(field.Index), fieldEncoders))));
                continue;
            }

            if (!ShouldIncludeRow(context.GenerationContext, reader))
            {
                continue;
            }

            rows.Add(new TableDataRow(
                rowIndex,
                context.GenerationContext.Fields
                    .Where(field => !field.IsIgnore)
                    .Select(field => EncodeCell(field, rowIndex, field.Index, reader.Read(field.Index), fieldEncoders))));
        }

        return new TableDataSnapshot(rows);
    }

    private TableDataSnapshot MaterializeMatrix(TableDataMaterializationContext context, Dictionary<XField, FieldEncoder> fieldEncoders)
    {
        var rows = new List<TableDataRow>();
        var key1Field = context.GenerationContext.GetField(DataMatrixTemplate.kKey1)
            ?? throw new InvalidOperationException($"DTGen=matrix 缺少 {DataMatrixTemplate.kKey1} 字段");
        var key2Field = context.GenerationContext.GetField(DataMatrixTemplate.kKey2)
            ?? throw new InvalidOperationException($"DTGen=matrix 缺少 {DataMatrixTemplate.kKey2} 字段");
        var valueField = context.GenerationContext.GetField(DataMatrixTemplate.kValue)
            ?? throw new InvalidOperationException($"DTGen=matrix 缺少 {DataMatrixTemplate.kValue} 字段");

        for (int rowIndex = context.FirstDataRowIndex; rowIndex <= context.Sheet.LastRowNum; rowIndex++)
        {
            var row = context.Sheet.GetRow(rowIndex);
            if (row == null || row.FirstCellNum < 0)
            {
                continue;
            }

            var reader = new RawRowReader(row, context.GetCellString);
            var rowReadErrors = context.GenerationContext.Fields
                .Where(field => !field.IsIgnore && reader.Read(field.Index).Error != null)
                .ToArray();
            if (rowReadErrors.Length > 0)
            {
                rows.Add(new TableDataRow(
                    rowIndex,
                    rowReadErrors.Select(field => EncodeCell(field, rowIndex, field.Index, reader.Read(field.Index), fieldEncoders))));
                continue;
            }

            if (!ShouldIncludeRow(context.GenerationContext, reader))
            {
                continue;
            }

            foreach (var pair in context.GenerationContext.ColumnIndexToKey2)
            {
                var rawValue = reader.Read(pair.Key);
                if (rawValue.Error == null
                    && (string.IsNullOrEmpty(rawValue.Value)
                        || rawValue.Value == context.GenerationContext.MatrixDefaultValue))
                {
                    if (rawValue.Value == context.GenerationContext.MatrixDefaultValue)
                    {
                        context.Diagnostics.GetMetrics(context.GenerationContext.FileName, context.GenerationContext.SheetName).MatrixDefaultSkippedCount++;
                    }
                    continue;
                }

                rows.Add(new TableDataRow(rowIndex,
                [
                    EncodeCell(key1Field, rowIndex, row.FirstCellNum, reader.Read(row.FirstCellNum), fieldEncoders),
                    EncodeCell(key2Field, context.FirstDataRowIndex - 1, pair.Key, RawCellValue.Virtual(pair.Value), fieldEncoders),
                    EncodeCell(valueField, rowIndex, pair.Key, rawValue, fieldEncoders),
                ]));
            }
        }

        return new TableDataSnapshot(rows);
    }

    private TableDataSnapshot MaterializeColumns(TableDataMaterializationContext context, Dictionary<XField, FieldEncoder> fieldEncoders)
    {
        var rows = new List<TableDataRow>();
        int maxLastCellNum = 0;
        for (int rowIndex = context.FirstDataRowIndex; rowIndex <= context.Sheet.LastRowNum; rowIndex++)
        {
            var row = context.Sheet.GetRow(rowIndex);
            if (row != null && row.LastCellNum > maxLastCellNum)
            {
                maxLastCellNum = row.LastCellNum;
            }
        }

        for (int columnIndex = context.GenerationContext.ColumnFirstDataColIndex; columnIndex < maxLastCellNum; columnIndex++)
        {
            if (context.GenerationContext.ColumnCommentRowIndex >= 0)
            {
                var markRow = context.Sheet.GetRow(context.GenerationContext.ColumnCommentRowIndex);
                var mark = ReadCell(markRow?.GetCell(columnIndex), context.GetCellString);
                if (mark.Error != null)
                {
                    var markerField = new XField(context.GenerationContext.ColumnCommentRowIndex)
                    {
                        Name = "#column",
                        TypeName = "string",
                    };
                    rows.Add(new TableDataRow(
                        sourceRowIndex: -1,
                        [EncodeCell(markerField, context.GenerationContext.ColumnCommentRowIndex, columnIndex, mark, fieldEncoders)]));
                    continue;
                }

                if (mark.Error == null
                    && !string.IsNullOrEmpty(mark.Value)
                    && mark.Value.TrimStart().StartsWith("#", StringComparison.Ordinal))
                {
                    context.Diagnostics.GetMetrics(context.GenerationContext.FileName, context.GenerationContext.SheetName).SkippedColumnCount++;
                    continue;
                }
            }

            var rawCells = context.GenerationContext.Fields
                .Where(field => !field.IsIgnore)
                .Select(field => (Field: field, Value: ReadCell(context.Sheet.GetRow(field.Index)?.GetCell(columnIndex), context.GetCellString)))
                .ToArray();
            if (!rawCells.Any(item => item.Value.Error != null || !string.IsNullOrEmpty(item.Value.Value)))
            {
                continue;
            }

            rows.Add(new TableDataRow(
                sourceRowIndex: -1,
                rawCells.Select(item => EncodeCell(item.Field, item.Field.Index, columnIndex, item.Value, fieldEncoders))));
        }

        return new TableDataSnapshot(rows);
    }

    private TableDataSnapshot MaterializeKeyValue(TableDataMaterializationContext context, Dictionary<XField, FieldEncoder> fieldEncoders)
    {
        var cells = context.GenerationContext.Fields
            .Where(field => !field.IsIgnore)
            .Select(field =>
            {
                var rowIndex = TryGetRowIndex(field.TypeCell);
                return EncodeCell(field, rowIndex, field.Index, RawCellValue.Virtual(field.Note), fieldEncoders);
            })
            .ToArray();

        return new TableDataSnapshot([new TableDataRow(sourceRowIndex: -1, cells)]);
    }

    private static bool ShouldIncludeRow(GenerationContext context, RawRowReader reader)
    {
        foreach (var field in context.Fields.Where(field => field.IsComment))
        {
            var value = reader.Read(field.Index);
            if (value.Error == null && value.Value == "#")
            {
                return false;
            }
        }

        return context.Fields
            .Where(field => !field.IsIgnore)
            .Select(field => reader.Read(field.Index))
            .Any(value => value.Error != null || !string.IsNullOrEmpty(value.Value));
    }

    private TableDataCell EncodeCell(
        XField field,
        int rowIndex,
        int columnIndex,
        RawCellValue rawCell,
        Dictionary<XField, FieldEncoder> fieldEncoders)
    {
        var cellReference = rowIndex >= 0 && columnIndex >= 0
            ? ParserUtils.GetCellAddress(rowIndex, columnIndex)
            : string.Empty;
        if (rawCell.Error != null)
        {
            return new TableDataCell(field, rowIndex, columnIndex, cellReference, rawCell.Value, TableDataCellState.Invalid, [], rawCell.Error);
        }

        var encoder = GetFieldEncoder(field, fieldEncoders);
        if (encoder.Error != null)
        {
            return new TableDataCell(field, rowIndex, columnIndex, cellReference, rawCell.Value, TableDataCellState.Invalid, [], encoder.Error);
        }

        try
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            encoder.Processor!.WriteToStream(writer, rawCell.Value);
            return new TableDataCell(field, rowIndex, columnIndex, cellReference, rawCell.Value, rawCell.State, stream.ToArray(), null);
        }
        catch (Exception exception)
        {
            return new TableDataCell(field, rowIndex, columnIndex, cellReference, rawCell.Value, TableDataCellState.Invalid, [], exception);
        }
    }

    private static FieldEncoder GetFieldEncoder(XField field, Dictionary<XField, FieldEncoder> fieldEncoders)
    {
        if (fieldEncoders.TryGetValue(field, out var encoder))
        {
            return encoder;
        }

        try
        {
            encoder = new FieldEncoder(DataTableProcessor.GetDataProcessorForSerialization(field.TypeName), null);
        }
        catch (Exception exception)
        {
            encoder = new FieldEncoder(null, exception);
        }

        fieldEncoders.Add(field, encoder);
        return encoder;
    }

    private static RawCellValue ReadCell(ICell? cell, Func<ICell?, string> getCellString)
    {
        try
        {
            var value = getCellString(cell);
            var state = cell == null
                ? TableDataCellState.Missing
                : string.IsNullOrEmpty(value)
                    ? TableDataCellState.Empty
                    : TableDataCellState.Parsed;
            return new RawCellValue(value, state, null);
        }
        catch (Exception exception)
        {
            return new RawCellValue(string.Empty, TableDataCellState.Invalid, exception);
        }
    }

    private static int TryGetRowIndex(string cellReference)
    {
        var match = s_CellRowRegex.Match(cellReference ?? string.Empty);
        return match.Success && int.TryParse(match.Value, out var oneBasedRow) ? oneBasedRow - 1 : -1;
    }

    private static int TryGetColumnIndex(string cellReference)
    {
        int columnIndex = 0;
        int letterCount = 0;
        foreach (var character in cellReference ?? string.Empty)
        {
            if (!char.IsLetter(character))
            {
                break;
            }

            columnIndex = (columnIndex * 26) + (char.ToUpperInvariant(character) - 'A' + 1);
            letterCount++;
        }

        return letterCount == 0 ? -1 : columnIndex - 1;
    }

    private sealed class RawRowReader
    {
        private readonly IRow m_Row;
        private readonly Func<ICell?, string> m_GetCellString;
        private readonly Dictionary<int, RawCellValue> m_Values = new();

        public RawRowReader(IRow row, Func<ICell?, string> getCellString)
        {
            m_Row = row;
            m_GetCellString = getCellString;
        }

        public RawCellValue Read(int columnIndex)
        {
            if (!m_Values.TryGetValue(columnIndex, out var value))
            {
                value = ReadCell(m_Row.GetCell(columnIndex), m_GetCellString);
                m_Values.Add(columnIndex, value);
            }

            return value;
        }
    }

    private sealed record FieldEncoder(DataTableProcessor.DataProcessor? Processor, Exception? Error);

    private sealed record RawCellValue(string Value, TableDataCellState State, Exception? Error)
    {
        public static RawCellValue Virtual(string value)
        {
            return new RawCellValue(
                value,
                string.IsNullOrEmpty(value) ? TableDataCellState.Empty : TableDataCellState.Parsed,
                null);
        }
    }
}
