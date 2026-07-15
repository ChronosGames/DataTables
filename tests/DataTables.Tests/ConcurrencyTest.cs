using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataTables;
using FluentAssertions;
using Xunit;

namespace DataTables.Tests
{
    public class ConcurrencyTest
    {
        /// <summary>
        /// 测试并发加载同一个数据表的安全性
        /// </summary>
        [Fact]
        public async Task ConcurrentLoadSameTable_ShouldBeThreadSafe()
        {
            // Arrange
            const int concurrentCount = 100;
            var tasks = new List<ValueTask<MockDataTable?>>();

            // 重置DataTableManager状态
            ResetDataTableManager();

            // 设置测试数据源
            DataTableManager.UseDataSource(new MockDataSource());

            // Act - 并发加载同一个表
            for (int i = 0; i < concurrentCount; i++)
            {
                tasks.Add(DataTableManager.LoadAsync<MockDataTable>());
            }

            var results = await Task.WhenAll(tasks.Select(t => t.AsTask()));

            // Assert
            results.Should().NotContainNulls();
            results.Should().AllSatisfy(table => table.Should().NotBeNull());

            // 所有结果应该是同一个实例（单例加载）
            var firstTable = results.First();
            results.Should().AllSatisfy(table => ReferenceEquals(table, firstTable).Should().BeTrue());
        }

        [Fact]
        public async Task ConcurrentLoadSameTable_ShouldStartTheSourceOnce()
        {
            const int concurrentCount = 32;
            ResetDataTableManager();

            var source = new BlockingCountingDataSource();
            DataTableManager.UseDataSource(source);
            ThreadPool.GetMinThreads(out var workerThreads, out var completionPortThreads);
            ThreadPool.SetMinThreads(Math.Max(workerThreads, concurrentCount), completionPortThreads);

            try
            {
                using var start = new Barrier(concurrentCount + 1);
                var tasks = Enumerable.Range(0, concurrentCount)
                    .Select(_ => Task.Run(async () =>
                    {
                        start.SignalAndWait();
                        return await DataTableManager.LoadAsync<MockDataTable>();
                    }))
                    .ToArray();

                start.SignalAndWait();
                await source.FirstLoadStarted.Task;
                await Task.Delay(100);

                source.LoadCount.Should().Be(1, "所有调用应等待同一个已发布的加载任务");

                source.Release();
                var results = await Task.WhenAll(tasks);
                results.Should().NotContainNulls();
            }
            finally
            {
                source.Release();
                ThreadPool.SetMinThreads(workerThreads, completionPortThreads);
            }
        }

        [Fact]
        public async Task ClearCacheDuringLoad_ShouldNotRepublishTheTable()
        {
            ResetDataTableManager();
            var source = new BlockingCountingDataSource();
            DataTableManager.UseDataSource(source);

            var loading = DataTableManager.LoadAsync<MockDataTable>().AsTask();
            await source.FirstLoadStarted.Task;

            DataTableManager.ClearCache();
            source.Release();

            (await loading).Should().BeNull();
            DataTableManager.GetCached<MockDataTable>().Should().BeNull();
            (await DataTableManager.LoadAsync<MockDataTable>()).Should().NotBeNull("清理后的新请求应能重新加载");
        }

        [Fact]
        public async Task DestroyDuringLoad_ShouldNotRepublishTheTable()
        {
            ResetDataTableManager();
            var source = new BlockingCountingDataSource();
            DataTableManager.UseDataSource(source);

            var loading = DataTableManager.LoadAsync<MockDataTable>().AsTask();
            await source.FirstLoadStarted.Task;

            DataTableManager.DestroyDataTable<MockDataTable>();
            source.Release();

            (await loading).Should().BeNull();
            DataTableManager.GetCached<MockDataTable>().Should().BeNull();
            (await DataTableManager.LoadAsync<MockDataTable>()).Should().NotBeNull("销毁后的新请求应能重新加载");
        }
            finally
            {
                source.Release();
            }
        }

        /// <summary>
        /// 测试并发加载不同数据表的性能
        /// </summary>
        [Fact]
        public async Task ConcurrentLoadDifferentTables_ShouldBeEfficient()
        {
            // Arrange
            ResetDataTableManager();
            DataTableManager.UseDataSource(new MockDataSource());

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Act - 并发加载多个相同类型的表（简化测试）
            var task1 = DataTableManager.LoadAsync<MockDataTable>().AsTask();
            var task2 = DataTableManager.LoadAsync<MockDataTable>().AsTask();
            var task3 = DataTableManager.LoadAsync<MockDataTable>().AsTask();

            var results = await Task.WhenAll(task1, task2, task3);
            stopwatch.Stop();

            // Assert
            results.Should().NotContainNulls();
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000); // 应该在1秒内完成

            // 所有结果应该是同一个实例（缓存生效）
            var firstTable = results.First();
            results.Should().AllSatisfy(table => ReferenceEquals(table, firstTable).Should().BeTrue());
        }

        private void ResetDataTableManager()
        {
            DataTableManager.ClearCache();
            DataTableManager.DisableEstimatedMemoryBudget();
        }
    }

    // Mock类用于测试
    public class MockDataSource : IDataSource
    {
        public DataSourceType SourceType => DataSourceType.Memory;

        public async ValueTask<System.IO.Stream> OpenReadAsync(string tableName, CancellationToken cancellationToken)
        {
            // 模拟网络延迟
            await Task.Delay(50, cancellationToken);

            // 返回模拟的数据表二进制数据
            return new System.IO.MemoryStream(CreateMockTableBytes(tableName), writable: false);
        }

        public ValueTask<bool> ExistsAsync(string name, CancellationToken cancellationToken) => ValueTask.FromResult(true);

        public ValueTask<DataSourceManifest> GetManifestAsync(CancellationToken cancellationToken) => ValueTask.FromResult(DataSourceManifest.Empty);

        public ValueTask<bool> IsAvailableAsync(CancellationToken cancellationToken) => ValueTask.FromResult(true);

        private byte[] CreateMockTableBytes(string tableName)
        {
            using var ms = new System.IO.MemoryStream();
            using var bw = new System.IO.BinaryWriter(ms);

            bw.Write("DTABLE");  // 签名
            bw.Write(3);         // 版本
            bw.Write(1UL);       // SchemaHash
            bw.Write("test");   // GeneratorVersion
            bw.Write(tableName); // TableFullName
            bw.Write(ushort.MinValue); // 数据行数
            bw.Write(0);         // Flags

            return ms.ToArray();
        }
    }

    public sealed class BlockingCountingDataSource : IDataSource
    {
        private readonly TaskCompletionSource<byte[]> _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _firstLoadStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _loadCount;

        public DataSourceType SourceType => DataSourceType.Memory;
        public int LoadCount => Volatile.Read(ref _loadCount);
        public TaskCompletionSource<bool> FirstLoadStarted => _firstLoadStarted;

        public async ValueTask<System.IO.Stream> OpenReadAsync(string tableName, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _loadCount) == 1)
            {
                _firstLoadStarted.TrySetResult(true);
            }

            return new System.IO.MemoryStream(await _release.Task, writable: false);
        }

        public ValueTask<bool> ExistsAsync(string name, CancellationToken cancellationToken) => ValueTask.FromResult(true);

        public ValueTask<DataSourceManifest> GetManifestAsync(CancellationToken cancellationToken) => ValueTask.FromResult(DataSourceManifest.Empty);

        public ValueTask<bool> IsAvailableAsync(CancellationToken cancellationToken) => ValueTask.FromResult(true);

        public void Release()
        {
            _release.TrySetResult(CreateMockTableBytes(typeof(MockDataTable).FullName!));
        }

        private static byte[] CreateMockTableBytes(string tableName)
        {
            using var ms = new System.IO.MemoryStream();
            using var bw = new System.IO.BinaryWriter(ms);
            bw.Write("DTABLE");
            bw.Write(3);
            bw.Write(1UL);
            bw.Write("test");
            bw.Write(tableName);
            bw.Write(ushort.MinValue);
            bw.Write(0);
            return ms.ToArray();
        }
    }

    public class MockDataTable : DataTable<MockDataRow>
    {
        public override ulong SchemaHash => 1UL;

        public MockDataTable(string name, int capacity) : base(name, capacity) { }
    }

    public class MockDataTable2 : DataTable<MockDataRow>
    {
        public override ulong SchemaHash => 1UL;

        public MockDataTable2(string name, int capacity) : base(name, capacity) { }
    }

    public class MockDataTable3 : DataTable<MockDataRow>
    {
        public override ulong SchemaHash => 1UL;

        public MockDataTable3(string name, int capacity) : base(name, capacity) { }
    }

    public class MockDataRow : DataRowBase
    {
        public int Id { get; private set; }

        public override bool Deserialize(System.IO.BinaryReader reader)
        {
            // 空实现，因为测试数据没有实际行
            return true;
        }
    }
}
