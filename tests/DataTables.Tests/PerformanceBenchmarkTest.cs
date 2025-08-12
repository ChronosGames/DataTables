using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        public async Task ConcurrentLoading_PerformanceBenchmark()
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
                tasks.Add(DataTableManager.GetDataTableAsync<MockDataTable>());
            }

            var results = await Task.WhenAll(tasks.Select(t => t.AsTask()));
            stopwatch.Stop();

            // Assert
            results.Should().NotContainNulls();
            results.Should().HaveCount(concurrentCount);

            // 性能验证：50个并发请求应在500ms内完成（留有余量）
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(500);

            // 所有结果应该是同一个实例（验证单例加载）
            var firstTable = results.First();
            results.Should().AllSatisfy(table => ReferenceEquals(table, firstTable).Should().BeTrue());

            Console.WriteLine($"并发加载性能: {concurrentCount}个请求耗时 {stopwatch.ElapsedMilliseconds}ms");
        }

        /// <summary>
        /// 基准测试：顺序加载性能对比
        /// </summary>
        [Fact]
        public async Task SequentialLoading_PerformanceBenchmark()
        {
            // Arrange
            ResetDataTableManager();
            DataTableManager.UseCustomSource(new FastMockDataSource());

            const int loadCount = 10;

            // Act & Measure
            var stopwatch = Stopwatch.StartNew();

            for (int i = 0; i < loadCount; i++)
            {
                var table = await DataTableManager.GetDataTableAsync<MockDataTable>();
                table.Should().NotBeNull();
            }

            stopwatch.Stop();

            // Assert - 顺序加载应该非常快（因为缓存）
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(100);
            Console.WriteLine($"顺序加载性能: {loadCount}个请求耗时 {stopwatch.ElapsedMilliseconds}ms");
        }

        /// <summary>
        /// 内存使用基准测试
        /// </summary>
        [Fact]
        public async Task MemoryUsage_Benchmark()
        {
            // Arrange
            ResetDataTableManager();
            DataTableManager.UseCustomSource(new FastMockDataSource());

            var memoryBefore = GC.GetTotalMemory(true);

            // Act - 加载多个数据表
            var table1 = await DataTableManager.GetDataTableAsync<MockDataTable>();
            var table2 = await DataTableManager.GetDataTableAsync<MockDataTable2>();
            var table3 = await DataTableManager.GetDataTableAsync<MockDataTable3>();

            var memoryAfter = GC.GetTotalMemory(false);
            var memoryUsed = memoryAfter - memoryBefore;

            // Assert
            table1.Should().NotBeNull();
            table2.Should().NotBeNull();
            table3.Should().NotBeNull();

            // 内存使用应该合理（小于10MB用于测试数据）
            memoryUsed.Should().BeLessThan(10 * 1024 * 1024); // 1MB

            Console.WriteLine($"内存使用: {memoryUsed / 1024}KB for 3 tables");
        }

        private void ResetDataTableManager()
        {
            // 清理测试状态
            var type = typeof(DataTableManager);
            var dataTablesField = type.GetField("s_DataTables",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var loadingTablesField = type.GetField("s_LoadingTables",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            if (dataTablesField?.GetValue(null) is System.Collections.Concurrent.ConcurrentDictionary<TypeNamePair, DataTableBase> dataTables)
                dataTables.Clear();

            if (loadingTablesField?.GetValue(null) is System.Collections.Concurrent.ConcurrentDictionary<TypeNamePair, Task<DataTableBase?>> loadingTables)
                loadingTables.Clear();
        }
    }

    // 快速Mock数据源 - 减少I/O延迟以便更好地测试并发性能
    public class FastMockDataSource : IDataSource
    {
        public DataSourceType SourceType => DataSourceType.Memory;

        public async ValueTask<byte[]> LoadAsync(string tableName)
        {
            // 最小延迟模拟
            await Task.Delay(1);
            return CreateMockTableBytes(tableName);
        }

        public ValueTask<bool> IsAvailableAsync()
        {
            return ValueTask.FromResult(true);
        }

        private byte[] CreateMockTableBytes(string tableName)
        {
            using var ms = new System.IO.MemoryStream();
            using var bw = new System.IO.BinaryWriter(ms);

            bw.Write("DTABLE");  // 签名
            bw.Write(1);         // 版本
            bw.Write7BitEncodedInt32(0); // 数据行数

            return ms.ToArray();
        }
    }
}
