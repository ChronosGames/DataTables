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
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

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
    public async Task Context_ShouldImplementModernContextContract()
    {
        using var context = new DataTableContext(new CountingSource());
        IDataTableContext manager = context;
        var table = await manager.LoadAsync<MockDataTable>();

        manager.Count.Should().Be(1);
        manager.IsLoaded<MockDataTable>().Should().BeTrue();
        manager.GetCached<MockDataTable>().Should().BeSameAs(table);
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
                async (target, cancellationToken) => await target.LoadAsync<MockDataTable>(string.Empty, cancellationToken))
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

    [Fact]
    public async Task NamedTable_ShouldUseTheSameRecommendedApiShape()
    {
        using var context = new DataTableContext(new CountingSource());

        var table = await context.LoadAsync<MockDataTable>("x001");

        context.GetCached<MockDataTable>("x001").Should().BeSameAs(table);
        context.IsLoaded<MockDataTable>("x001").Should().BeTrue();
        context.GetCached<MockDataTable>().Should().BeNull();
    }

    [Fact]
    public async Task CallerCancellation_ShouldCancelOnlyThatWaiterAndPreserveSharedLoad()
    {
        var source = new CancellableSource();
        using var context = new DataTableContext(source);
        using var callerCancellation = new CancellationTokenSource();
        var canceledWaiter = context.LoadAsync<MockDataTable>(string.Empty, callerCancellation.Token).AsTask();
        await source.Started.Task.WaitAsync(TestTimeout);
        var survivingWaiter = context.LoadAsync<MockDataTable>().AsTask();

        callerCancellation.Cancel();

        Func<Task> canceledAction = async () => await canceledWaiter;
        await canceledAction.Should().ThrowAsync<OperationCanceledException>();
        source.CancellationObserved.Task.IsCompleted.Should().BeFalse(
            "a caller token cancels its waiter, not the shared lifecycle load");
        source.LoadCount.Should().Be(1);

        source.Release();
        var loaded = await survivingWaiter.WaitAsync(TestTimeout);

        loaded.Should().NotBeNull();
        context.GetCached<MockDataTable>().Should().BeSameAs(loaded);
        source.LoadCount.Should().Be(1);
    }

    [Fact]
    public async Task ClearCache_ShouldCancelTheContextLifecycleRead()
    {
        var source = new CancellableSource();
        using var context = new DataTableContext(source);
        var loading = context.LoadAsync<MockDataTable>().AsTask();
        await source.Started.Task.WaitAsync(TestTimeout);

        context.ClearCache();

        (await loading.WaitAsync(TestTimeout)).Should().BeNull();
        await source.CancellationObserved.Task.WaitAsync(TestTimeout);
    }

    [Fact]
    public async Task Dispose_ShouldCancelTheContextLifecycleRead()
    {
        var source = new CancellableSource();
        var context = new DataTableContext(source);
        var loading = context.LoadAsync<MockDataTable>().AsTask();
        await source.Started.Task.WaitAsync(TestTimeout);

        context.Dispose();

        (await loading.WaitAsync(TestTimeout)).Should().BeNull();
        await source.CancellationObserved.Task.WaitAsync(TestTimeout);
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
            DataTableBinaryProtocol.WriteHeader(writer, 1UL, "test", typeof(MockDataTable).FullName!);
            return new MemoryStream(stream.ToArray(), writable: false);
        }

        public ValueTask<bool> ExistsAsync(string name, CancellationToken cancellationToken) => ValueTask.FromResult(true);
        public ValueTask<DataSourceManifest> GetManifestAsync(CancellationToken cancellationToken) => ValueTask.FromResult(DataSourceManifest.Empty);
        public ValueTask<bool> IsAvailableAsync(CancellationToken cancellationToken) => ValueTask.FromResult(true);
    }

    private sealed class CancellableSource : IDataSource
    {
        private readonly TaskCompletionSource<byte[]> m_Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int m_LoadCount;

        public DataSourceType SourceType => DataSourceType.Memory;
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> CancellationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int LoadCount => Volatile.Read(ref m_LoadCount);

        public async ValueTask<Stream> OpenReadAsync(string name, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref m_LoadCount);
            Started.TrySetResult(true);
            try
            {
                var payload = await m_Release.Task.WaitAsync(cancellationToken);
                return new MemoryStream(payload, writable: false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                CancellationObserved.TrySetResult(true);
                throw;
            }
        }

        public void Release()
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            DataTableBinaryProtocol.WriteHeader(writer, 1UL, "test", typeof(MockDataTable).FullName!);
            m_Release.TrySetResult(stream.ToArray());
        }

        public ValueTask<bool> ExistsAsync(string name, CancellationToken cancellationToken) => ValueTask.FromResult(true);
        public ValueTask<DataSourceManifest> GetManifestAsync(CancellationToken cancellationToken) => ValueTask.FromResult(DataSourceManifest.Empty);
        public ValueTask<bool> IsAvailableAsync(CancellationToken cancellationToken) => ValueTask.FromResult(true);
    }
}
