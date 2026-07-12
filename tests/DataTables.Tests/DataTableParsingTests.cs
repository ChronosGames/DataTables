using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DataTables;
using FluentAssertions;
using Xunit;

namespace DataTables.Tests;

public sealed class DataTableParsingTests
{
    [Fact]
    public async Task BackgroundThreadParsing_ShouldNotBlockTheCallingContext()
    {
        using var release = new ManualResetEventSlim();
        using var started = new ManualResetEventSlim();
        try
        {
            ParsingThreadRow.OnDeserialize = () =>
            {
                started.Set();
                release.Wait(TimeSpan.FromSeconds(5));
            };
            using var context = new DataTableContext(new ParsingThreadSource())
            {
                ParseExecution = DataTableParseExecution.BackgroundThread
            };

            var stopwatch = Stopwatch.StartNew();
            var loading = context.LoadAsync<ParsingThreadTable>().AsTask();
            stopwatch.Stop();

            stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
            started.Wait(TimeSpan.FromSeconds(2)).Should().BeTrue();
            release.Set();
            (await loading).Should().NotBeNull();
        }
        finally
        {
            release.Set();
            ParsingThreadRow.OnDeserialize = null;
        }
    }

    [Fact]
    public async Task CallingContextParsing_ShouldRunInlineAfterCompletedIo()
    {
        var callerThread = Environment.CurrentManagedThreadId;
        var parseThread = 0;
        try
        {
            ParsingThreadRow.OnDeserialize = () => parseThread = Environment.CurrentManagedThreadId;
            using var context = new DataTableContext(new ParsingThreadSource())
            {
                ParseExecution = DataTableParseExecution.CallingContext
            };

            await context.LoadAsync<ParsingThreadTable>();

            parseThread.Should().Be(callerThread);
        }
        finally
        {
            ParsingThreadRow.OnDeserialize = null;
        }
    }
}

public sealed class ParsingThreadTable : DataTable<ParsingThreadRow>
{
    public ParsingThreadTable(string name, int capacity) : base(name, capacity)
    {
    }

    public override ulong SchemaHash => 7UL;
}

public sealed class ParsingThreadRow : DataRowBase
{
    public static Action? OnDeserialize { get; set; }

    public int Value { get; private set; }

    public override bool Deserialize(BinaryReader reader)
    {
        OnDeserialize?.Invoke();
        Value = reader.ReadInt32();
        return true;
    }
}

public sealed class ParsingThreadSource : IDataSource
{
    public DataSourceType SourceType => DataSourceType.Memory;

    public ValueTask<Stream> OpenReadAsync(string name, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        var rowCountPosition = DataTableBinaryProtocol.WriteHeader(writer, 7UL, "test", name);
        writer.Write(42);
        DataTableBinaryProtocol.PatchRowCount(writer, rowCountPosition, 1);
        return new ValueTask<Stream>(new MemoryStream(stream.ToArray(), writable: false));
    }

    public ValueTask<bool> ExistsAsync(string name, CancellationToken cancellationToken) => ValueTask.FromResult(true);
    public ValueTask<DataSourceManifest> GetManifestAsync(CancellationToken cancellationToken) => ValueTask.FromResult(DataSourceManifest.Empty);
    public ValueTask<bool> IsAvailableAsync(CancellationToken cancellationToken) => ValueTask.FromResult(true);
}
