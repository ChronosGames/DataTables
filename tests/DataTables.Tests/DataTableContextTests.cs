using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace DataTables.Tests;

public sealed class DataTableContextTests
{
    [Fact]
    public async Task Contexts_ShouldOwnIndependentSourcesLoadsAndCaches()
    {
        var firstSource = new CountingSource();
        var secondSource = new CountingSource();
        using var first = new DataTableContext(firstSource);
        using var second = new DataTableContext(secondSource);

        var firstLoads = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => first.LoadAsync<MockDataTable>().AsTask()));
        var secondTable = await second.LoadAsync<MockDataTable>();

        firstLoads.Should().OnlyContain(table => ReferenceEquals(table, firstLoads[0]));
        firstLoads[0].Should().NotBeSameAs(secondTable);
        firstSource.LoadCount.Should().Be(1);
        secondSource.LoadCount.Should().Be(1);

        first.ClearCache();

        first.GetCached<MockDataTable>().Should().BeNull();
        second.GetCached<MockDataTable>().Should().BeSameAs(secondTable);
    }

    [Fact]
    public async Task ChangingSource_ShouldInvalidateOnlyThatContext()
    {
        var original = new CountingSource();
        var replacement = new CountingSource();
        using var context = new DataTableContext(original);
        var originalTable = await context.LoadAsync<MockDataTable>();

        context.UseDataSource(replacement);
        var replacementTable = await context.LoadAsync<MockDataTable>();

        replacementTable.Should().NotBeSameAs(originalTable);
        original.LoadCount.Should().Be(1);
        replacement.LoadCount.Should().Be(1);
    }

    [Fact]
    public async Task DisposedContext_ShouldRejectFurtherOperations()
    {
        var context = new DataTableContext(new CountingSource());
        await context.LoadAsync<MockDataTable>();

        context.Dispose();

        var action = async () => await context.LoadAsync<MockDataTable>();
        await action.Should().ThrowAsync<ObjectDisposedException>();
        context.Invoking(value => value.GetCached<MockDataTable>()).Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task Context_ShouldImplementInstanceManagerContract()
    {
        using var context = new DataTableContext(new CountingSource());
        IDataTableManager manager = context;
        var table = await context.LoadAsync<MockDataTable>();

        manager.Count.Should().Be(1);
        manager.HasDataTable<MockDataTable>().Should().BeTrue();
        manager.GetDataTable<MockDataTable>().Should().BeSameAs(table);
        manager.GetAllDataTables().Should().ContainSingle().Which.Should().BeSameAs(table);
        manager.DestroyDataTable(table!).Should().BeTrue();
        manager.Count.Should().Be(0);
    }

    [Fact]
    public async Task ContextAwareRegistration_ShouldPreheatTheReceivingContext()
    {
        var source = new CountingSource();
        using var context = new DataTableContext(source);
        context.RegisterTables(new[]
        {
            new TableRegistration(
                typeof(MockDataTable),
                string.Empty,
                Priority.Critical,
                async (target, cancellationToken) => await target.CreateDataTableAsync<MockDataTable>(string.Empty, cancellationToken))
        });

        var stats = await context.PreheatAsync(Priority.Critical);

        stats.TableCount.Should().Be(1);
        stats.LoadedCount.Should().Be(1);
        context.GetCached<MockDataTable>().Should().NotBeNull();
        source.LoadCount.Should().Be(1);
    }

    [Fact]
    public async Task StaticManager_ShouldBeACompatibilityFacadeOverDefaultContext()
    {
        DataTableManager.ClearCache();
        DataTableManager.UseDataSource(new CountingSource());

        var table = await DataTableManager.LoadAsync<MockDataTable>();

        DataTableManager.GetCached<MockDataTable>().Should().BeSameAs(table);
        DataTableManager.Count.Should().Be(1);
        DataTableManager.ClearCache();
    }

    private sealed class CountingSource : IDataSource
    {
        private int m_LoadCount;

        public DataSourceType SourceType => DataSourceType.Memory;
        public int LoadCount => Volatile.Read(ref m_LoadCount);

        public async ValueTask<Stream> OpenReadAsync(string name, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref m_LoadCount);
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            DataTableBinaryProtocol.WriteHeader(writer, 1UL, "test", name);
            return new MemoryStream(stream.ToArray(), writable: false);
        }

        public ValueTask<bool> ExistsAsync(string name, CancellationToken cancellationToken) => ValueTask.FromResult(true);
        public ValueTask<DataSourceManifest> GetManifestAsync(CancellationToken cancellationToken) => ValueTask.FromResult(DataSourceManifest.Empty);
        public ValueTask<bool> IsAvailableAsync(CancellationToken cancellationToken) => ValueTask.FromResult(true);
    }
}
