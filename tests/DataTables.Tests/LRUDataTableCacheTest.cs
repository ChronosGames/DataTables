using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace DataTables.Tests
{
    public class LRUDataTableCacheTest
    {
        /// <summary>
        /// 测试LRU缓存基本功能
        /// </summary>
        [Fact]
        public void LRUCache_BasicOperations_ShouldWork()
        {
            // Arrange
            var cache = new LRUDataTableCache(1024 * 1024); // 1MB限制
            var key = new TypeNamePair(typeof(MockDataTable), "test");
            var table = CreateMockTable("test", 100);

            // Act & Assert - 初始状态
            cache.TryGet<MockDataTable>(key, out var result).Should().BeFalse();
            result.Should().BeNull();

            // 添加到缓存
            cache.Set(key, table);
            cache.TryGet<MockDataTable>(key, out var cachedResult).Should().BeTrue();
            cachedResult.Should().BeSameAs(table);

            // 统计信息
            var stats = cache.GetStats();
            stats.TotalItems.Should().Be(1);
            stats.MemoryUsage.Should().BeGreaterThan(0);
            stats.HitRate.Should().Be(0.5f); // 1次命中，1次未命中
        }

        /// <summary>
        /// 测试LRU淘汰机制
        /// </summary>
        [Fact]
        public void LRUCache_EvictionPolicy_ShouldWorkCorrectly()
        {
            // Arrange - 创建有限容量缓存，能容纳大约2个表
            var cache = new LRUDataTableCache(60 * 1024); // 60KB限制，能容纳约2个表

            var key1 = new TypeNamePair(typeof(MockDataTable), "table1");
            var key2 = new TypeNamePair(typeof(MockDataTable), "table2");
            var key3 = new TypeNamePair(typeof(MockDataTable), "table3");

            var table1 = CreateMockTable("table1", 100);
            var table2 = CreateMockTable("table2", 100);
            var table3 = CreateMockTable("table3", 100);

            // Act - 添加表，超出容量限制
            cache.Set(key1, table1);
            cache.Set(key2, table2);
            cache.Set(key3, table3); // 这应该触发LRU淘汰

            // Assert - 最旧的项应该被淘汰
            cache.TryGet<MockDataTable>(key1, out _).Should().BeFalse("最旧的项应该被淘汰");
            cache.TryGet<MockDataTable>(key2, out _).Should().BeTrue("较新的项应该保留");
            cache.TryGet<MockDataTable>(key3, out _).Should().BeTrue("最新的项应该保留");

            var stats = cache.GetStats();
            stats.TotalItems.Should().BeLessThan(3, "应该有项被淘汰");
        }

        /// <summary>
        /// 测试LRU访问顺序更新
        /// </summary>
        [Fact]
        public void LRUCache_AccessOrder_ShouldUpdateCorrectly()
        {
            // Arrange
            var cache = new LRUDataTableCache(60 * 1024); // 60KB限制，能容纳约2个表

            var key1 = new TypeNamePair(typeof(MockDataTable), "table1");
            var key2 = new TypeNamePair(typeof(MockDataTable), "table2");
            var key3 = new TypeNamePair(typeof(MockDataTable), "table3");

            // Act - 按顺序添加
            cache.Set(key1, CreateMockTable("table1", 100));
            cache.Set(key2, CreateMockTable("table2", 100));

            // 访问第一个表（更新其访问时间）
            cache.TryGet<MockDataTable>(key1, out _);

            // 添加第三个表，应该淘汰key2（未被最近访问）
            cache.Set(key3, CreateMockTable("table3", 100));

            // Assert - key1应该保留（最近被访问），key2应该被淘汰
            cache.TryGet<MockDataTable>(key1, out _).Should().BeTrue("最近访问的项应该保留");
            cache.TryGet<MockDataTable>(key2, out _).Should().BeFalse("未访问的项应该被淘汰");
            cache.TryGet<MockDataTable>(key3, out _).Should().BeTrue("新添加的项应该保留");
        }

        /// <summary>
        /// 测试缓存清理
        /// </summary>
        [Fact]
        public void LRUCache_Clear_ShouldEmptyCache()
        {
            // Arrange
            var cache = new LRUDataTableCache(1024 * 1024);
            var key = new TypeNamePair(typeof(MockDataTable), "test");

            cache.Set(key, CreateMockTable("test", 100));
            cache.TryGet<MockDataTable>(key, out _).Should().BeTrue();

            // Act
            cache.Clear();

            // Assert
            cache.TryGet<MockDataTable>(key, out _).Should().BeFalse();
            var stats = cache.GetStats();
            stats.TotalItems.Should().Be(0);
            stats.MemoryUsage.Should().Be(0);
        }

        /// <summary>
        /// 测试内存管理器集成
        /// </summary>
        [Fact]
        public async Task MemoryManager_Integration_ShouldWork()
        {
            // Arrange
            ResetDataTableManager();
            DataTableManager.UseCustomSource(new FastMockDataSource());
            DataTableManager.EnableMemoryManagement(10); // 10MB限制

            // Act
            var table1 = await DataTableManager.LoadAsync<MockDataTable>();
            var table2 = await DataTableManager.LoadAsync<MockDataTable2>();

            // Assert
            table1.Should().NotBeNull();
            table2.Should().NotBeNull();

            var cacheStats = DataTableManager.GetCacheStats();
            cacheStats.Should().NotBeNull();
            cacheStats!.Value.TotalItems.Should().BeGreaterThan(0);

            // Cleanup
            DataTableManager.ClearCache();
            DataTableManager.DisableMemoryManagement();
        }

        /// <summary>
        /// 测试缓存统计信息
        /// </summary>
        [Fact]
        public void LRUCache_Stats_ShouldProvideAccurateInformation()
        {
            // Arrange
            var cache = new LRUDataTableCache(1024 * 1024);
            var key1 = new TypeNamePair(typeof(MockDataTable), "table1");
            var key2 = new TypeNamePair(typeof(MockDataTable), "table2");

            // Act - 进行一些操作来生成统计数据
            cache.TryGet<MockDataTable>(key1, out _); // 未命中
            cache.Set(key1, CreateMockTable("table1", 100));
            cache.TryGet<MockDataTable>(key1, out _); // 命中
            cache.TryGet<MockDataTable>(key2, out _); // 未命中

            var stats = cache.GetStats();

            // Assert
            stats.TotalItems.Should().Be(1);
            stats.MemoryUsage.Should().BeGreaterThan(0);
            stats.AccessCount.Should().Be(3); // 3次访问
            stats.HitCount.Should().Be(1);    // 1次命中
            stats.HitRate.Should().Be(1.0f / 3.0f); // 33%命中率
            stats.MemoryUsageRate.Should().BeLessThan(1.0f); // 未满
        }

        private MockDataTable CreateMockTable(string name, int capacity)
        {
            return new MockDataTable(name, capacity);
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
}
