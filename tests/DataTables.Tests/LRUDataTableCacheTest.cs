using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace DataTables.Tests
{
    public class LRUDataTableCacheTest
    {
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void LRUCache_ShouldRejectNonPositiveLimits(long maxMemoryBytes)
        {
            Action action = () => new LRUDataTableCache(maxMemoryBytes);

            action.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void LRUCache_ShouldUseInjectedSizeProviderForEstimatedBudget()
        {
            var cache = new LRUDataTableCache(10, _ => 6);
            var firstKey = new TypeNamePair(typeof(MockDataTable), "first");
            var secondKey = new TypeNamePair(typeof(MockDataTable), "second");

            cache.TrySet(firstKey, CreateMockTable("first", 1)).Should().BeTrue();
            cache.TrySet(secondKey, CreateMockTable("second", 1)).Should().BeTrue();

            cache.Contains(firstKey).Should().BeFalse("the injected estimate requires eviction");
            cache.Contains(secondKey).Should().BeTrue();
            cache.EstimatedMemoryBudgetBytes.Should().Be(10);
            cache.EstimatedMemoryUsageBytes.Should().Be(6);
            cache.GetStats().EstimatedMemoryUsageBytes.Should().Be(6);
        }

        [Fact]
        public void LRUCache_ShouldRejectOversizedTableWithoutEvictingExistingEntries()
        {
            var cache = new LRUDataTableCache(10, table => table.Name == "oversized" ? 11 : 6);
            var existingKey = new TypeNamePair(typeof(MockDataTable), "existing");
            var oversizedKey = new TypeNamePair(typeof(MockDataTable), "oversized");
            cache.Set(existingKey, CreateMockTable("existing", 1));

            cache.TrySet(oversizedKey, CreateMockTable("oversized", 1)).Should().BeFalse();
            cache.Set(oversizedKey, CreateMockTable("oversized", 1));

            cache.Contains(existingKey).Should().BeTrue("a rejected item must not disturb the current LRU set");
            cache.Contains(oversizedKey).Should().BeFalse("Set preserves compatibility by silently skipping oversized items");
            cache.EstimatedMemoryUsageBytes.Should().Be(6);
        }

        [Fact]
        public void LRUCache_ShouldRejectInvalidSizeProviderResults()
        {
            var cache = new LRUDataTableCache(10, _ => 0);

            Action action = () => cache.TrySet(
                new TypeNamePair(typeof(MockDataTable), "invalid"),
                CreateMockTable("invalid", 1));

            action.Should().Throw<InvalidOperationException>().WithMessage("*positive byte count*");
        }

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
            stats.EstimatedMemoryUsageBytes.Should().BeGreaterThan(0);
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
            stats.EstimatedMemoryUsageBytes.Should().Be(0);
        }

        /// <summary>
        /// 测试内存管理器集成
        /// </summary>
        [Fact]
        public async Task MemoryManager_Integration_ShouldWork()
        {
            // Arrange - ensure clean state and verify memory management works
            ResetDataTableManager();
            DataTableManager.UseDataSource(new FastMockDataSource());
            
            // Test the core functionality: enable memory management and load tables
            DataTableManager.EnableEstimatedMemoryBudget(10); // 10MB估算预算
            DataTableManager.IsEstimatedMemoryBudgetEnabled.Should().BeTrue();

            // Act - load tables which should work regardless of cache implementation details
            var table1 = await DataTableManager.LoadAsync<MockDataTable>();
            var table2 = await DataTableManager.LoadAsync<MockDataTable2>();

            // Assert - focus on the core functionality rather than cache internals
            table1.Should().NotBeNull();
            table2.Should().NotBeNull();
            
            // Verify memory management is still enabled (main requirement)
            DataTableManager.IsEstimatedMemoryBudgetEnabled.Should().BeTrue();
            
            // Verify we can get the same tables again (caching behavior)
            var cachedTable1 = DataTableManager.GetCached<MockDataTable>();
            var cachedTable2 = DataTableManager.GetCached<MockDataTable2>();
            
            cachedTable1.Should().NotBeNull("Table should be cached after loading");
            cachedTable2.Should().NotBeNull("Table should be cached after loading");

            // Cleanup
            DataTableManager.ClearCache();
            DataTableManager.DisableEstimatedMemoryBudget();
        }

        [Fact]
        public async Task MemoryManager_ShouldNotCacheTablesLargerThanEstimatedBudget()
        {
            ResetDataTableManager();
            DataTableManager.UseDataSource(new SizedMockDataSource(5000));
            DataTableManager.EnableEstimatedMemoryBudget(1);

            await DataTableManager.LoadAsync<MockDataTable>();
            var latestTable = await DataTableManager.LoadAsync<MockDataTable2>();

            latestTable.Should().NotBeNull("oversized tables can still be used by the loading caller");
            DataTableManager.GetCached<MockDataTable>().Should().BeNull();
            DataTableManager.GetCached<MockDataTable2>().Should().BeNull();
            DataTableManager.Count.Should().Be(0);
            DataTableManager.GetCacheStats()!.Value.EstimatedMemoryUsageBytes.Should().Be(0);

            DataTableManager.ClearCache();
            DataTableManager.DisableEstimatedMemoryBudget();
        }

        [Fact]
        public async Task DestroyDataTable_ShouldRemoveTheMemoryManagedEntry()
        {
            ResetDataTableManager();
            DataTableManager.UseDataSource(new FastMockDataSource());
            DataTableManager.EnableEstimatedMemoryBudget(10);
            await DataTableManager.LoadAsync<MockDataTable>();

            DataTableManager.DestroyDataTable<MockDataTable>().Should().BeTrue();

            DataTableManager.GetCached<MockDataTable>().Should().BeNull();
            DataTableManager.Count.Should().Be(0);
            DataTableManager.GetCacheStats()!.Value.TotalItems.Should().Be(0);

            DataTableManager.ClearCache();
            DataTableManager.DisableEstimatedMemoryBudget();
        }

        [Fact]
        public async Task ClearCache_ShouldNotReturnMemoryManagedTables()
        {
            ResetDataTableManager();
            DataTableManager.UseDataSource(new FastMockDataSource());
            DataTableManager.EnableEstimatedMemoryBudget(10);
            await DataTableManager.LoadAsync<MockDataTable>();

            DataTableManager.ClearCache();

            DataTableManager.GetCached<MockDataTable>().Should().BeNull();
            DataTableManager.Count.Should().Be(0);

            DataTableManager.DisableEstimatedMemoryBudget();
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
            stats.EstimatedMemoryUsageBytes.Should().BeGreaterThan(0);
            stats.AccessCount.Should().Be(3); // 3次访问
            stats.HitCount.Should().Be(1);    // 1次命中
            stats.HitRate.Should().Be(1.0f / 3.0f); // 33%命中率
            stats.EstimatedBudgetUsageRate.Should().BeLessThan(1.0f); // 未满
        }

        private MockDataTable CreateMockTable(string name, int capacity)
        {
            return new MockDataTable(name, capacity);
        }

        private void ResetDataTableManager()
        {
            // 清理测试状态 - 使用公共API来确保状态一致性
            DataTableManager.ClearCache();
            DataTableManager.DisableEstimatedMemoryBudget();
        }

        private sealed class SizedMockDataSource : IDataSource
        {
            private readonly ushort _rowCount;

            public SizedMockDataSource(ushort rowCount)
            {
                _rowCount = rowCount;
            }

            public DataSourceType SourceType => DataSourceType.Memory;

            public ValueTask<System.IO.Stream> OpenReadAsync(string tableName, CancellationToken cancellationToken)
            {
                using var ms = new System.IO.MemoryStream();
                using var bw = new System.IO.BinaryWriter(ms);
                bw.Write("DTABLE");
                bw.Write(3);
                bw.Write(1UL);
                bw.Write("test");
                bw.Write(tableName);
                bw.Write(_rowCount);
                bw.Write(0);
                return ValueTask.FromResult<System.IO.Stream>(new System.IO.MemoryStream(ms.ToArray(), writable: false));
            }

            public ValueTask<bool> ExistsAsync(string name, CancellationToken cancellationToken) => ValueTask.FromResult(true);

            public ValueTask<DataSourceManifest> GetManifestAsync(CancellationToken cancellationToken) => ValueTask.FromResult(DataSourceManifest.Empty);

            public ValueTask<bool> IsAvailableAsync(CancellationToken cancellationToken) => ValueTask.FromResult(true);
        }
    }
}
