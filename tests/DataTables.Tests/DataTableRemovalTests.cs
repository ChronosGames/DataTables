using System.Collections.Generic;
using System.IO;
using DataTables;
using FluentAssertions;
using Xunit;

namespace DataTables.Tests;

public sealed class DataTableRemovalTests
{
    [Fact]
    public void RemoveAllDataRows_ShouldClearRowsAndDerivedIndexes()
    {
        var table = new IndexedTable(string.Empty, 2);
        table.Add(0, new IndexedRow(10));
        table.Add(1, new IndexedRow(20));

        table.RemoveAllDataRows();

        table.Count.Should().Be(0);
        table.GetAllDataRows().Should().BeEmpty();
        table.Find(10).Should().BeNull();
    }

    [Fact]
    public void MatrixRemoveAllDataRows_ShouldClearAllEntries()
    {
        var table = new TestMatrix(string.Empty, 1);
        table.Add(0, 1, 2, 3);

        table.RemoveAllDataRows();

        table.Count.Should().Be(0);
        table.GetAllDataRows().Should().BeEmpty();
    }

    private sealed class IndexedTable : DataTable<IndexedRow>
    {
        private readonly Dictionary<int, IndexedRow> m_Index = new();

        public IndexedTable(string name, int capacity) : base(name, capacity)
        {
        }

        public void Add(int index, IndexedRow row)
        {
            InternalAddDataRow(index, row);
            m_Index.Add(row.Id, row);
        }

        public IndexedRow? Find(int id) => m_Index.TryGetValue(id, out var row) ? row : null;

        protected override void OnDataRowsRemoved()
        {
            m_Index.Clear();
        }
    }

    private sealed class IndexedRow : DataRowBase
    {
        public IndexedRow()
        {
        }

        public IndexedRow(int id)
        {
            Id = id;
        }

        public int Id { get; private set; }

        public override bool Deserialize(BinaryReader reader)
        {
            Id = reader.ReadInt32();
            return true;
        }
    }

    private sealed class TestMatrix : DataMatrixBase<int, int, int>
    {
        public TestMatrix(string name, int capacity) : base(name, capacity)
        {
        }

        public void Add(int index, int key1, int key2, int value) => SetDataRow(index, key1, key2, value);

        protected override MatrixDataRowBase<int, int, int> CreateDataRowInstance() => new TestMatrixRow();
    }

    private sealed class TestMatrixRow : MatrixDataRowBase<int, int, int>
    {
        public override bool Deserialize(BinaryReader reader) => true;
    }
}
