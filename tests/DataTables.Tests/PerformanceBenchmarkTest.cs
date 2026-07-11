using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataTables;
using FluentAssertions;
using Xunit;

namespace DataTables.Tests
{
    public class PerformanceBenchmarkTest
    {
        /// <summary>
        /// 基准测试：并发加载性能
        /// </summary>
        [Fact]
        public async Task ConcurrentLoading_ShouldUseSingleFlight()
        {
            // Arrange
            ResetDataTableManager();
            DataTableManager.UseCustomSource(new FastMockDataSource());

            const int concurrentCount = 50;
            var tasks = new List<ValueTask<MockDataTable?>>();

            // Act & Measure
            var stopwatch = Stopwatch.StartNew();

            for (int i = 0; i < concurrentCount; i++)
            {
                tasks.Add(DataTableManager.GetOrCreateDataTableAsync<MockDataTable>());
            }

            var results = await Task.WhenAll(tasks.Select(t => t.AsTask()));
            stopwatch.Stop();

            // Assert
            results.Should().NotContainNulls();
            results.Should().HaveCount(concurrentCount);

            // 所有结果应该是同一个实例（验证单例加载）
            var firstTable = results.First();
            results.Should().AllSatisfy(table => ReferenceEquals(table, firstTable).Should().BeTrue());

            Console.WriteLine($"并发加载性能: {concurrentCount}个请求耗时 {stopwatch.ElapsedMilliseconds}ms");
        }

        /// <summary>
        /// 基准测试：顺序加载性能对比
        /// </summary>
        [Fact]
        public async Task SequentialLoading_ShouldReturnCachedInstance()
        {
            // Arrange
            ResetDataTableManager();
            DataTableManager.UseCustomSource(new FastMockDataSource());

            const int loadCount = 10;
            MockDataTable? firstTable = null;

            // Act & Measure
            var stopwatch = Stopwatch.StartNew();

            for (int i = 0; i < loadCount; i++)
            {
                var table = await DataTableManager.GetOrCreateDataTableAsync<MockDataTable>();
                table.Should().NotBeNull();
                firstTable ??= table;
                table.Should().BeSameAs(firstTable);
            }

            stopwatch.Stop();

            Console.WriteLine($"顺序加载性能: {loadCount}个请求耗时 {stopwatch.ElapsedMilliseconds}ms");
        }

        /// <summary>
        /// 内存使用基准测试
        /// </summary>
        [Fact]
        public async Task LoadingThreeTables_ShouldSucceed()
        {
            // Arrange
            ResetDataTableManager();
            DataTableManager.UseCustomSource(new FastMockDataSource());

            var memoryBefore = GC.GetTotalMemory(true);

            // Act - 加载多个数据表
            var table1 = await DataTableManager.GetOrCreateDataTableAsync<MockDataTable>();
            var table2 = await DataTableManager.GetOrCreateDataTableAsync<MockDataTable2>();
            var table3 = await DataTableManager.GetOrCreateDataTableAsync<MockDataTable3>();

            var memoryAfter = GC.GetTotalMemory(false);
            var memoryUsed = memoryAfter - memoryBefore;

            // Assert
            table1.Should().NotBeNull();
            table2.Should().NotBeNull();
            table3.Should().NotBeNull();

            Console.WriteLine($"内存使用: {memoryUsed / 1024}KB for 3 tables");
        }

        /// <summary>
        /// 基准测试：直接行添加路径应避免反射调用产生的逐行分配。
        /// </summary>
        [Fact]
        public void DirectAddDataRow_ShouldAllocateLessThanReflection()
        {
            const int rowCount = 10_000;
            var rows = Enumerable.Range(0, rowCount).Select(_ => new MockDataRow()).ToArray();
            var directTable = new AddDataRowBenchmarkTable("direct", rowCount);
            var reflectionTable = new AddDataRowBenchmarkTable("reflection", rowCount);
            var internalAddMethod = typeof(AddDataRowBenchmarkTable).GetMethod(
                "InternalAddDataRow",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

            // Warm up JIT and reflection metadata paths before measuring.
            directTable.DirectAdd(0, rows[0]);
            internalAddMethod.Invoke(reflectionTable, new object[] { 0, rows[0] });

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var directAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var directStopwatch = Stopwatch.StartNew();
            for (var i = 0; i < rowCount; i++)
            {
                directTable.DirectAdd(i, rows[i]);
            }
            directStopwatch.Stop();
            var directAllocated = GC.GetAllocatedBytesForCurrentThread() - directAllocatedBefore;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var reflectionAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var reflectionStopwatch = Stopwatch.StartNew();
            for (var i = 0; i < rowCount; i++)
            {
                internalAddMethod.Invoke(reflectionTable, new object[] { i, rows[i] });
            }
            reflectionStopwatch.Stop();
            var reflectionAllocated = GC.GetAllocatedBytesForCurrentThread() - reflectionAllocatedBefore;

            directAllocated.Should().BeLessThan(reflectionAllocated);

            Console.WriteLine(
                $"行添加性能: direct={directStopwatch.ElapsedMilliseconds}ms/{directAllocated}B, " +
                $"reflection={reflectionStopwatch.ElapsedMilliseconds}ms/{reflectionAllocated}B");
        }

        private void ResetDataTableManager()
        {
            DataTableManager.ClearCache();
            DataTableManager.DisableMemoryManagement();
            DataTableManager.ClearTableRegistrations();
            DataTableManager.ClearHooks();
        }
    }

    // 快速Mock数据源 - 减少I/O延迟以便更好地测试并发性能
    public class FastMockDataSource : IDataSource
    {
        public DataSourceType SourceType => DataSourceType.Memory;

        public async ValueTask<System.IO.Stream> OpenReadAsync(string tableName, CancellationToken cancellationToken)
        {
            // 最小延迟模拟
            await Task.Delay(1, cancellationToken);
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

    public class AddDataRowBenchmarkTable : DataTable<MockDataRow>
    {
        public AddDataRowBenchmarkTable(string name, int capacity) : base(name, capacity) { }

        public void DirectAdd(int index, MockDataRow dataRow)
        {
            InternalAddDataRow(index, dataRow);
        }
    }
}
