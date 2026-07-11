using System.Collections.Generic;
using System.IO;
using System.Linq;
using DataTables;
using FluentAssertions;
using Xunit;

namespace DataTables.Tests;

public class DataTableQueryTests
{
    [Fact]
    public void GetDataRows_Comparison_ShouldSortACopyWithoutChangingTableOrder()
    {
        var table = CreateTable(3, 1, 2);

        var result = table.GetDataRows((left, right) => left.Value.CompareTo(right.Value));

        result.Select(row => row.Value).Should().Equal(1, 2, 3);
        table.GetAllDataRows().Select(row => row.Value).Should().Equal(3, 1, 2);
    }

    [Fact]
    public void GetDataRows_FilterAndComparison_ShouldUseTheComparison()
    {
        var table = CreateTable(3, 1, 2, 4);

        var result = table
            .GetDataRows(row => row.Value % 2 == 0, (left, right) => right.Value.CompareTo(left.Value))
            .Select(row => row.Value);

        result.Should().Equal(4, 2);
    }

    [Fact]
    public void GetAllDataRows_ShouldNotExposeTheBackingArray()
    {
        var table = CreateTable(1, 2);
        var rows = table.GetAllDataRows();

        rows[0] = new TestRow { Value = 99 };

        table[0].Value.Should().Be(1);
    }

    private static TestTable CreateTable(params int[] values)
    {
        var table = new TestTable("test", values.Length);
        for (var index = 0; index < values.Length; index++)
        {
            table.Add(index, values[index]);
        }

        return table;
    }

    private sealed class TestTable : DataTable<TestRow>
    {
        public TestTable(string name, int capacity) : base(name, capacity)
        {
        }

        public void Add(int index, int value) => InternalAddDataRow(index, new TestRow { Value = value });
    }

    private sealed class TestRow : DataRowBase
    {
        public int Value { get; set; }

        public override bool Deserialize(BinaryReader reader) => true;
    }
}
