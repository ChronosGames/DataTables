using System;
using System.Collections.Generic;
using System.Linq;

namespace DataTables.GeneratorCore;

internal enum TableDataCellState
{
    Missing,
    Empty,
    Parsed,
    Invalid,
}

internal sealed class TableDataCell
{
    private readonly byte[] m_EncodedValue;

    public TableDataCell(
        XField field,
        int rowIndex,
        int columnIndex,
        string cellReference,
        string rawValue,
        TableDataCellState state,
        byte[] encodedValue,
        Exception? error)
    {
        FieldName = field.Name;
        TypeName = field.TypeName;
        RowIndex = rowIndex;
        ColumnIndex = columnIndex;
        CellReference = cellReference;
        RawValue = rawValue;
        State = state;
        m_EncodedValue = encodedValue;
        Error = error;
    }

    public string FieldName { get; }

    public string TypeName { get; }

    public int RowIndex { get; }

    public int ColumnIndex { get; }

    public string CellReference { get; }

    public string RawValue { get; }

    public TableDataCellState State { get; }

    public ReadOnlyMemory<byte> EncodedValue => m_EncodedValue;

    public Exception? Error { get; }
}

internal sealed class TableDataRow
{
    private readonly IReadOnlyList<TableDataCell> m_Cells;

    public TableDataRow(int sourceRowIndex, IEnumerable<TableDataCell> cells)
    {
        SourceRowIndex = sourceRowIndex;
        m_Cells = Array.AsReadOnly(cells.ToArray());
    }

    public int SourceRowIndex { get; }

    public IReadOnlyList<TableDataCell> Cells => m_Cells;

    public TableDataCell? GetCell(string fieldName)
    {
        return m_Cells.FirstOrDefault(cell => string.Equals(cell.FieldName, fieldName, StringComparison.Ordinal));
    }
}

internal sealed class TableDataSnapshot
{
    private readonly IReadOnlyList<TableDataRow> m_Rows;

    public TableDataSnapshot(IEnumerable<TableDataRow> rows)
    {
        m_Rows = Array.AsReadOnly(rows.ToArray());
        EstimatedSizeInBytes = m_Rows.Sum(row => row.Cells.Sum(cell =>
            (long)cell.EncodedValue.Length + (cell.RawValue.Length * sizeof(char))));
    }

    public IReadOnlyList<TableDataRow> Rows => m_Rows;

    public IEnumerable<TableDataCell> Cells => m_Rows.SelectMany(row => row.Cells);

    public long EstimatedSizeInBytes { get; }
}
