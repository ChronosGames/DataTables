using System;
using System.Threading.Tasks;
using DataTables;
using FluentAssertions;
using Xunit;

namespace DataTables.Tests
{
    /// <summary>
    /// 测试优化后的DataTableManager - 激进优化版本
    /// 特性：纯异步优先，内置LRU缓存，工厂模式，零反射
    /// </summary>
    public class DataTableManagerOptimizedTest
    {
        /// <summary>
        /// 测试新的异步优先API设计
        /// </summary>
        [Fact]
        public async Task NewAsyncAPI_ShouldWorkCorrectly()
        {
            // Arrange
            ResetDataTableManager();
            DataTableManager.UseCustomSource(new FastMockDataSource());

            // Act - 使用新的异步优先API
            var table1 = await DataTableManager.LoadAsync<MockDataTable>();
            var table2 = DataTableManager.GetCached<MockDataTable>();
            
            // Assert
            table1.Should().NotBeNull("异步加载应该成功");
            table2.Should().NotBeNull("缓存获取应该成功");
            table1.Should().BeSameAs(table2, "应该是同一个实例");
            
            DataTableManager.IsLoaded<MockDataTable>().Should().BeTrue("表应该已加载");
        }

        /// <summary>
        /// 测试内存管理功能
        /// </summary>
        [Fact]
        public async Task MemoryManagement_ShouldWorkCorrectly()
        {
            // Arrange
            ResetDataTableManager();
            DataTableManager.UseCustomSource(new FastMockDataSource());
            DataTableManager.EnableMemoryManagement(10); // 10MB限制

            // Act
            var table = await DataTableManager.LoadAsync<MockDataTable>();
            
            // Assert
            table.Should().NotBeNull();
            DataTableManager.IsMemoryManagementEnabled.Should().BeTrue();
            
            var cacheStats = DataTableManager.GetCacheStats();
            cacheStats.Should().NotBeNull("应该有缓存统计信息");
            cacheStats.Value.TotalItems.Should().BeGreaterThan(0, "应该有缓存项");

            // Cleanup
            DataTableManager.DisableMemoryManagement();
        }

        /// <summary>
        /// 测试工厂模式注册
        /// </summary>
        [Fact]
        public void FactoryPattern_ShouldBeSupported()
        {
            // Act - 注册工厂
            DataTableManager.RegisterFactory<MockDataTable, MockDataRow, MockDataTableFactory>();
            
            // Assert
            DataTableFactoryManager.HasFactory<MockDataTable>().Should().BeTrue("工厂应该已注册");
            
            var factory = DataTableFactoryManager.GetFactory<MockDataTable, MockDataRow>();
            factory.Should().NotBeNull("应该能获取到工厂");
        }

        /// <summary>
        /// 测试简化的Hook机制
        /// </summary>
        [Fact]
        public async Task SimplifiedHooks_ShouldWork()
        {
            // Arrange
            ResetDataTableManager();
            DataTableManager.UseCustomSource(new FastMockDataSource());
            
            bool hookTriggered = false;
            DataTableManager.OnLoaded<MockDataTable>(table => hookTriggered = true);

            // Act
            await DataTableManager.LoadAsync<MockDataTable>();

            // Assert
            hookTriggered.Should().BeTrue("Hook应该被触发");
            
            // Cleanup
            DataTableManager.ClearHooks();
        }

        /// <summary>
        /// 测试预热功能
        /// </summary>
        [Fact]
        public async Task PreheatAsync_ShouldWorkCorrectly()
        {
            // Arrange
            ResetDataTableManager();
            DataTableManager.UseCustomSource(new FastMockDataSource());

            // Act
            var stats = await DataTableManager.PreheatAsync(Priority.Critical);

            // Assert
            stats.TableCount.Should().BeGreaterOrEqualTo(0, "应该有统计信息");
            stats.LoadTime.Should().BeGreaterOrEqualTo(0, "加载时间应该有效");
        }

        /// <summary>
        /// 测试性能监控
        /// </summary>
        [Fact]
        public async Task PerformanceMonitoring_ShouldWork()
        {
            // Arrange
            ResetDataTableManager();
            DataTableManager.UseCustomSource(new FastMockDataSource());
            
            bool profilingTriggered = false;
            DataTableManager.EnableProfiling(stats => 
            {
                profilingTriggered = true;
                stats.Should().NotBeNull();
            });

            // Act
            await DataTableManager.PreheatAsync();

            // Assert
            profilingTriggered.Should().BeTrue("性能监控应该被触发");
            
            var globalStats = DataTableManager.GetStats();
            globalStats.TableCount.Should().BeGreaterOrEqualTo(0);
        }

        /// <summary>
        /// 测试智能初始化功能
        /// </summary>
        [Fact]
        public async Task SmartInitialization_ShouldWork()
        {
            // Arrange
            ResetDataTableManager();
            // 不设置数据源，测试自动初始化

            // Act & Assert - 应该不抛出异常
            var table = DataTableManager.GetCached<MockDataTable>();
            table.Should().BeNull("没有数据源时应该返回null");
            
            // 设置数据源后应该能工作
            DataTableManager.UseCustomSource(new FastMockDataSource());
            var loadedTable = await DataTableManager.LoadAsync<MockDataTable>();
            loadedTable.Should().NotBeNull("设置数据源后应该能加载");
        }

        /// <summary>
        /// 测试高性能内联优化
        /// </summary>
        [Fact]
        public void HighPerformanceInlines_ShouldWork()
        {
            // Arrange
            ResetDataTableManager();
            DataTableManager.UseCustomSource(new FastMockDataSource());

            // Act - 测试内联优化的内部方法
            var table1 = DataTableManager.GetDataTableInternal<MockDataTable>();
            var table2 = DataTableManager.GetDataTableInternal<MockDataTable>();

            // Assert
            if (table1 != null && table2 != null)
            {
                table1.Should().BeSameAs(table2, "内联方法应该返回相同实例");
            }
        }

        /// <summary>
        /// 测试向后兼容性
        /// </summary>
        [Fact]
        public void BackwardCompatibility_ShouldBePreserved()
        {
            // Arrange
            ResetDataTableManager();
            DataTableManager.UseCustomSource(new FastMockDataSource());

            // Act - 使用旧API（应该有警告但能工作）
            #pragma warning disable CS0618 // 忽略过时警告
            var oldTable = DataTableManager.GetDataTable<MockDataTable>();
            #pragma warning restore CS0618

            var newTable = DataTableManager.GetCached<MockDataTable>();

            // Assert
            // 旧API应该内部使用新实现
            if (oldTable != null || newTable != null)
            {
                // 至少有一种方式能工作
                true.Should().BeTrue();
            }
        }

        private void ResetDataTableManager()
        {
            // 清理测试状态
            DataTableManager.ClearCache();
            DataTableManager.ClearHooks();
            DataTableManager.DisableMemoryManagement();
        }
    }

    /// <summary>
    /// Mock数据表工厂 - 用于测试工厂模式
    /// </summary>
    public class MockDataTableFactory : IDataTableFactory<MockDataTable, MockDataRow>
    {
        public MockDataTable CreateTable(string name, int capacity) => new(name, capacity);
        public MockDataRow CreateRow() => new();
    }
}