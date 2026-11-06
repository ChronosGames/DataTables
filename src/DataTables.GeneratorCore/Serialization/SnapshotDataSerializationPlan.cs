using System;
using System.IO;

namespace DataTables.GeneratorCore;

internal interface ITableDataSerializationPlan
{
    int WriteDataRows(TableDataSnapshot snapshot, BinaryWriter writer);
}

internal sealed class SnapshotDataSerializationPlan : ITableDataSerializationPlan
{
    public int WriteDataRows(TableDataSnapshot snapshot, BinaryWriter writer)
    {
        foreach (var row in snapshot.Rows)
        {
            foreach (var cell in row.Cells)
            {
                if (cell.State == TableDataCellState.Invalid)
                {
                    throw new InvalidOperationException($"Cannot serialize invalid materialized cell '{cell.CellReference}'.");
                }

                writer.Write(cell.EncodedValue.Span);
            }
        }

        return snapshot.Rows.Count;
    }
}
