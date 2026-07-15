using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DataTables;
using FluentAssertions;
using Xunit;

namespace DataTables.Tests;

public sealed class SplitTableQueryTests
{
    [Fact]
    public async Task StaticQuery_ShouldSelectTheRequestedSplitTableName()
    {
        DataTableManager.ClearCache();
        DataTableManager.UseDataSource(new SplitTableSource());

        try
        {
            await DataTableManager.LoadAsync<SplitQueryTable>("x001");
            await DataTableManager.LoadAsync<SplitQueryTable>("x002");

            SplitQueryTable.GetById("x001", 1)!.Value.Should().Be("x001");
            SplitQueryTable.GetById("x002", 1)!.Value.Should().Be("x002");
            SplitQueryTable.GetById(1).Should().BeNull("the unnamed table was not loaded");
        }
        finally
        {
            DataTableManager.ClearCache();
        }
    }
}

public sealed class SplitQueryTable : DataTable<SplitQueryRow>
{
    private readonly Dictionary<int, SplitQueryRow> m_ById = new();

    public SplitQueryTable(string name, int capacity) : base(name, capacity)
    {
    }

    public override ulong SchemaHash => 31UL;

    protected override void InternalAddDataRow(int index, SplitQueryRow dataRow)
    {
        base.InternalAddDataRow(index, dataRow);
        m_ById.Add(dataRow.Id, dataRow);
    }

    protected override void OnDataRowsRemoved()
    {
        m_ById.Clear();
    }

    public static SplitQueryRow? GetById(int id) => GetById(string.Empty, id);

    public static SplitQueryRow? GetById(string dataTableName, int id)
    {
        var table = DataTableManager.GetCached<SplitQueryTable>(dataTableName);
        return table?.m_ById.TryGetValue(id, out var row) == true ? row : null;
    }
}

public sealed class SplitQueryRow : DataRowBase
{
    public int Id { get; private set; }
    public string Value { get; private set; } = string.Empty;

    public override bool Deserialize(BinaryReader reader)
    {
        Id = reader.ReadInt32();
        Value = reader.ReadString();
        return true;
    }
}

internal sealed class SplitTableSource : IDataSource
{
    public DataSourceType SourceType => DataSourceType.Memory;

    public ValueTask<Stream> OpenReadAsync(string name, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var childName = name.EndsWith(".x001", StringComparison.Ordinal) ? "x001" : "x002";
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        var rowCountPosition = DataTableBinaryProtocol.WriteHeader(writer, 31UL, "test", typeof(SplitQueryTable).FullName!);
        writer.Write(1);
        writer.Write(childName);
        DataTableBinaryProtocol.PatchRowCount(writer, rowCountPosition, 1);
        return ValueTask.FromResult<Stream>(new MemoryStream(stream.ToArray(), writable: false));
    }

    public ValueTask<bool> ExistsAsync(string name, CancellationToken cancellationToken) => ValueTask.FromResult(true);
    public ValueTask<DataSourceManifest> GetManifestAsync(CancellationToken cancellationToken) => ValueTask.FromResult(DataSourceManifest.Empty);
    public ValueTask<bool> IsAvailableAsync(CancellationToken cancellationToken) => ValueTask.FromResult(true);
}
