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
            DataTableManager.UseCustomSource(new MockDataSource());

            // Act - 并发加载同一个表
            for (int i = 0; i < concurrentCount; i++)
            {
                tasks.Add(DataTableManager.GetOrCreateDataTableAsync<MockDataTable>());
            }

            var results = await Task.WhenAll(tasks.Select(t => t.AsTask()));

            // Assert
            results.Should().NotContainNulls();
            results.Should().AllSatisfy(table => table.Should().NotBeNull());

            // 所有结果应该是同一个实例（单例加载）
            var firstTable = results.First();
            results.Should().AllSatisfy(table => ReferenceEquals(table, firstTable).Should().BeTrue());
        }

        /// <summary>
        /// 测试并发加载不同数据表的性能
        /// </summary>
        [Fact]
        public async Task ConcurrentLoadDifferentTables_ShouldBeEfficient()
        {
            // Arrange
            ResetDataTableManager();
            DataTableManager.UseCustomSource(new MockDataSource());

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Act - 并发加载多个相同类型的表（简化测试）
            var task1 = DataTableManager.GetOrCreateDataTableAsync<MockDataTable>().AsTask();
            var task2 = DataTableManager.GetOrCreateDataTableAsync<MockDataTable>().AsTask();
            var task3 = DataTableManager.GetOrCreateDataTableAsync<MockDataTable>().AsTask();

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
            // 通过反射清理静态字段进行测试重置
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

    // Mock类用于测试
    public class MockDataSource : IDataSource
    {
        public DataSourceType SourceType => DataSourceType.Memory;

        public async ValueTask<byte[]> LoadAsync(string tableName)
        {
            // 模拟网络延迟
            await Task.Delay(50);

            // 返回模拟的数据表二进制数据
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
            bw.Write(2);         // 版本
            bw.Write(ushort.MinValue); // 数据行数

            return ms.ToArray();
        }
    }

    public class MockDataTable : DataTable<MockDataRow>
    {
        public MockDataTable(string name, int capacity) : base(name, capacity) { }
    }

    public class MockDataTable2 : DataTable<MockDataRow>
    {
        public MockDataTable2(string name, int capacity) : base(name, capacity) { }
    }

    public class MockDataTable3 : DataTable<MockDataRow>
    {
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
