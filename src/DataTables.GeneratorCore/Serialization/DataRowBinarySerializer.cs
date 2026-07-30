using System;
using System.IO;
using System.Linq;
using NPOI.SS.UserModel;

namespace DataTables.GeneratorCore;

internal sealed class DataRowBinarySerializer
{
    private readonly GenerationContext m_Context;
    private readonly int m_FirstDataRowIndex;
    private readonly DiagnosticsCollector m_Diagnostics;
    private readonly Func<ICell?, string> m_GetCellString;
    private readonly Func<IRow?, bool> m_ShouldWriteDataRow;

    public DataRowBinarySerializer(
        GenerationContext context,
        int firstDataRowIndex,
        DiagnosticsCollector diagnostics,
        Func<ICell?, string> getCellString,
        Func<IRow?, bool> shouldWriteDataRow)
    {
        m_Context = context;
        m_FirstDataRowIndex = firstDataRowIndex;
        m_Diagnostics = diagnostics;
        m_GetCellString = getCellString;
        m_ShouldWriteDataRow = shouldWriteDataRow;
    }

    public int WriteDataRows(ISheet sheet, BinaryWriter writer)
    {
        int dataRowCount = 0;

        if (m_Context.DataSetType == "kv") return WriteKvBytes(writer);

        if (m_Context.DataSetType == "column")
        {
            int maxLastCellNum = 0;
            for (int r = m_FirstDataRowIndex; r <= sheet.LastRowNum; r++)
            {
                var row = sheet.GetRow(r);
                if (row == null) continue;
                if (row.LastCellNum > maxLastCellNum) maxLastCellNum = row.LastCellNum;
            }

            for (int c = m_Context.ColumnFirstDataColIndex; c < maxLastCellNum; c++)
            {
                if (m_Context.ColumnCommentRowIndex >= 0)
                {
                    var markRow = sheet.GetRow(m_Context.ColumnCommentRowIndex);
                    var mk = m_GetCellString(markRow?.GetCell(c));
                    if (!string.IsNullOrEmpty(mk) && mk.TrimStart().StartsWith("#", StringComparison.Ordinal))
                    {
                        m_Diagnostics.GetMetrics(m_Context.FileName, m_Context.SheetName).SkippedColumnCount++;
                        continue;
                    }
                }

                bool hasAnyValue = false;
                foreach (var field in m_Context.Fields)
                {
                    if (field.IsIgnore) continue;
                    var row = sheet.GetRow(field.Index);
                    var text = m_GetCellString(row?.GetCell(c));
                    if (!string.IsNullOrEmpty(text))
                    {
                        hasAnyValue = true;
                        break;
                    }
                }
                if (!hasAnyValue) continue;

                dataRowCount += WriteColumnBytes(writer, sheet, c);
            }
        }
        else
        {
            for (int i = m_FirstDataRowIndex; i <= sheet.LastRowNum; i++)
            {
                var row = sheet.GetRow(i);
                if (!m_ShouldWriteDataRow(row)) continue;
                dataRowCount += WriteRowBytes(writer, row!);
            }
        }

        return dataRowCount;
    }

    private int WriteKvBytes(BinaryWriter writer)
    {
        foreach (var field in m_Context.Fields.Where(x => !x.IsIgnore))
        {
            var processor = GetDataProcessorWithDiagnostics(field);
            try
            {
                processor.WriteToStream(writer, field.Note);
            }
            catch (Exception e)
            {
                throw new FormatException($"解析 kv 配置 {field.Name} 的值时出错: {field.Note}", e);
            }
        }

        return 1;
    }

    private int WriteRowBytes(BinaryWriter writer, IRow row)
    {
        if (m_Context.DataSetType == "matrix")
        {
            return WriteMatrixRowBytes(writer, row);
        }

        foreach (var field in m_Context.Fields)
        {
            if (field.IsIgnore) continue;
            var processor = GetDataProcessorWithDiagnostics(field);
            try
            {
                processor.WriteToStream(writer, m_GetCellString(row.GetCell(field.Index)));
            }
            catch (Exception e)
            {
                throw new FormatException($"解析单元格内容时出错: {GetRowColString(row.RowNum, field.Index)}", e);
            }
        }

        return 1;
    }

    private int WriteMatrixRowBytes(BinaryWriter writer, IRow row)
    {
        var processor1 = DataTableProcessor.GetDataProcessorForSerialization(m_Context.GetField(DataMatrixTemplate.kKey1)!.TypeName);
        var processor2 = DataTableProcessor.GetDataProcessorForSerialization(m_Context.GetField(DataMatrixTemplate.kKey2)!.TypeName);
        var processor3 = DataTableProcessor.GetDataProcessorForSerialization(m_Context.GetField(DataMatrixTemplate.kValue)!.TypeName);
        int dataRowCount = 0;

        foreach (var pair in m_Context.ColumnIndexToKey2)
        {
            var cellString = m_GetCellString(row.GetCell(pair.Key));
            if (string.IsNullOrEmpty(cellString) || cellString == m_Context.MatrixDefaultValue)
            {
                if (cellString == m_Context.MatrixDefaultValue)
                {
                    m_Diagnostics.GetMetrics(m_Context.FileName, m_Context.SheetName).MatrixDefaultSkippedCount++;
                }
                continue;
            }

            dataRowCount++;
            WriteCell(writer, processor1, m_GetCellString(row.GetCell(row.FirstCellNum)), row.RowNum, row.FirstCellNum);
            WriteCell(writer, processor2, pair.Value, 1, pair.Key);
            WriteCell(writer, processor3, cellString, row.RowNum, pair.Key);
        }

        return dataRowCount;
    }

    private int WriteColumnBytes(BinaryWriter writer, ISheet sheet, int columnIndex)
    {
        foreach (var field in m_Context.Fields)
        {
            if (field.IsIgnore) continue;
            var processor = GetDataProcessorWithDiagnostics(field);
            try
            {
                var row = sheet.GetRow(field.Index);
                processor.WriteToStream(writer, m_GetCellString(row?.GetCell(columnIndex)));
            }
            catch (Exception e)
            {
                throw new FormatException($"解析单元格内容时出错: {GetRowColString(field.Index, columnIndex)}", e);
            }
        }

        return 1;
    }

    private static void WriteCell(BinaryWriter writer, DataTableProcessor.DataProcessor processor, string value, int row, int column)
    {
        try
        {
            processor.WriteToStream(writer, value);
        }
        catch (Exception e)
        {
            throw new FormatException($"解析单元格内容时出错: {GetRowColString(row, column)}", e);
        }
    }

    private DataTableProcessor.DataProcessor GetDataProcessorWithDiagnostics(XField field)
    {
        try
        {
            return DataTableProcessor.GetDataProcessorForSerialization(field.TypeName);
        }
        catch (DataTableProcessor.DataTypeParseException ex)
        {
            var cell = string.IsNullOrEmpty(field.TypeCell) ? string.Empty : field.TypeCell;
            m_Diagnostics.Error(m_Context.FileName, m_Context.SheetName, cell, $"字段 '{field.Name}' 类型 '{field.TypeName}' 解析失败: {ex.Message}");
            throw new FormatException($"类型解析失败: File='{m_Context.FileName}', Sheet='{m_Context.SheetName}', Field='{field.Name}', TypeCell='{cell}', FieldType='{field.TypeName}'. {ex.Message}", ex);
        }
    }

    private static string GetRowColString(int row, int col) => string.Format("{0}{1}", ConvertToDigit(col), row + 1);

    private static string ConvertToDigit(int num)
    {
        if (num < 0) throw new ArgumentOutOfRangeException(nameof(num));
        return num < 26 ? Convert.ToString((char)('A' + num)) : ConvertToDigit(num / 26 - 1) + ConvertToDigit(num % 26);
    }
}
