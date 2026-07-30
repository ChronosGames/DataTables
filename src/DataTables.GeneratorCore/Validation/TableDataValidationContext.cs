using System;
using NPOI.SS.UserModel;

namespace DataTables.GeneratorCore;

internal sealed class TableDataValidationContext
{
    private readonly Func<ICell?, string> m_GetCellString;
    private readonly Func<IRow?, bool> m_ShouldWriteDataRow;

    public TableDataValidationContext(
        GenerationContext schema,
        ISheet sheet,
        int firstDataRowIndex,
        Func<ICell?, string> getCellString,
        Func<IRow?, bool> shouldWriteDataRow)
    {
        Schema = schema;
        Sheet = sheet;
        FirstDataRowIndex = firstDataRowIndex;
        m_GetCellString = getCellString;
        m_ShouldWriteDataRow = shouldWriteDataRow;
    }

    public GenerationContext Schema { get; }

    public ISheet Sheet { get; }

    public int FirstDataRowIndex { get; }

    public string GetCellString(ICell? cell) => m_GetCellString(cell);

    public bool ShouldValidateRow(IRow? row) => m_ShouldWriteDataRow(row);

    public static string GetCellReference(int row, int column)
    {
        return $"{ConvertToColumnName(column)}{row + 1}";
    }

    private static string ConvertToColumnName(int column)
    {
        if (column < 0) throw new ArgumentOutOfRangeException(nameof(column));
        return column < 26
            ? Convert.ToString((char)('A' + column))
            : ConvertToColumnName(column / 26 - 1) + ConvertToColumnName(column % 26);
    }
}
