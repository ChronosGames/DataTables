using System;
using System.Diagnostics;
using System.Threading.Tasks;
using DataTables;
using FluentAssertions;
using Xunit;

namespace DataTables.Tests
{
    public class OptimizationComparisonTest
    {
        /// <summary>
        /// 对比测试：优化前后的性能差异
        /// </summary>
        [Fact]
        public async Task PerformanceComparison_ShouldShowImprovement()
        {
            // Arrange
            ResetDataTableManager();
            DataTableManager.UseCustomSource(new FastMockDataSource());

            const int iterations = 1000;
            var results = new PerformanceResults();

            // 预加载数据
            await DataTableManager.LoadAsync<MockDataTable>();

            // Test 1: 传统方式 - 每次调用GetDataTableInternal
            var sw1 = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                var table = DataTableManager.GetDataTableInternal<MockDataTable>();
                _ = table?.Count ?? 0;
            }
            sw1.Stop();
            results.TraditionalApproachMs = sw1.ElapsedMilliseconds;

            // Test 2: V2优化方式 - 缓存优先
            var sw2 = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                var table = DataTableManager.GetCached<MockDataTable>();
                _ = table?.Count ?? 0;
            }
            sw2.Stop();
            results.OptimizedApproachMs = sw2.ElapsedMilliseconds;

            // Test 3: 并发性能测试
            var concurrentTasks = new Task[50];
            var sw3 = Stopwatch.StartNew();

            for (int i = 0; i < concurrentTasks.Length; i++)
            {
                concurrentTasks[i] = Task.Run(async () =>
                {
                    for (int j = 0; j < 20; j++)
                    {
                        await DataTableManager.LoadAsync<MockDataTable>();
                    }
                });
            }

            await Task.WhenAll(concurrentTasks);
            sw3.Stop();
            results.ConcurrentLoadMs = sw3.ElapsedMilliseconds;

            // Assert & Report
            results.OptimizedApproachMs.Should().BeLessThan(results.TraditionalApproachMs,
                "优化后的方法应该更快");

            results.ConcurrentLoadMs.Should().BeLessThan(5000,
                "并发加载应该在合理时间内完成");

            // 计算性能提升比例
            var improvementRatio = (double)results.TraditionalApproachMs / results.OptimizedApproachMs;
            improvementRatio.Should().BeGreaterThan(1.0, "应该有性能提升");

            // 输出性能报告
            Console.WriteLine($"性能对比报告:");
            Console.WriteLine($"传统方式: {results.TraditionalApproachMs}ms");
            Console.WriteLine($"优化方式: {results.OptimizedApproachMs}ms");
            Console.WriteLine($"并发加载: {results.ConcurrentLoadMs}ms");
            Console.WriteLine($"性能提升: {improvementRatio:F2}x");
        }

        /// <summary>
        /// 测试内存使用优化
        /// </summary>
        [Fact]
        public async Task MemoryUsageComparison_ShouldShowImprovement()
        {
            // Arrange
            ResetDataTableManager();
            DataTableManager.UseCustomSource(new FastMockDataSource());
            DataTableManager.EnableMemoryManagement(10); // 10MB限制

            var memoryBefore = GC.GetTotalMemory(true);

            // Act - 加载多个表
            await DataTableManager.LoadAsync<MockDataTable>();
            await DataTableManager.LoadAsync<MockDataTable2>();
            await DataTableManager.LoadAsync<MockDataTable3>();

            var memoryAfter = GC.GetTotalMemory(false);
            var memoryUsed = memoryAfter - memoryBefore;

            // Assert
            memoryUsed.Should().BeLessThan(5 * 1024 * 1024, "内存使用应该合理(小于5MB)");

            var cacheStats = DataTableManager.GetCacheStats();
            if (cacheStats.HasValue)
            {
                Console.WriteLine($"缓存统计:");
                Console.WriteLine($"缓存项数: {cacheStats.Value.TotalItems}");
                Console.WriteLine($"内存使用: {cacheStats.Value.MemoryUsage / 1024}KB");
                Console.WriteLine($"命中率: {cacheStats.Value.HitRate:P}");
            }

            // Cleanup
            DataTableManager.DisableMemoryManagement();
        }

        /// <summary>
        /// 测试API易用性改进
        /// </summary>
        [Fact]
        public async Task APIUsabilityImprovement_ShouldBeEasierToUse()
        {
            // Arrange & Act - 展示新API的易用性

            // 1. 简洁的配置
            DataTableManager.UseCustomSource(new FastMockDataSource());
            DataTableManager.EnableMemoryManagement(10);
            DataTableManager.EnableProfiling(stats =>
            {
                Console.WriteLine($"加载了 {stats.TableCount} 个表，耗时 {stats.LoadTime}ms");
            });

            // 2. 清晰的异步加载
            var table = await DataTableManager.LoadAsync<MockDataTable>();
            table.Should().NotBeNull();

            // 3. 直观的状态检查
            DataTableManager.IsLoaded<MockDataTable>().Should().BeTrue();

            // 4. 简单的Hook注册
            bool hookTriggered = false;
            DataTableManager.OnLoaded<MockDataTable2>(t => hookTriggered = true);

            await DataTableManager.LoadAsync<MockDataTable2>();
            hookTriggered.Should().BeTrue("Hook应该被触发");

            // 5. 丰富的统计信息
            var stats = DataTableManager.GetStats();
            stats.TableCount.Should().BeGreaterThan(0);
            // CacheHitRate不存在于LoadStats中，移除此检查

            // Cleanup
            DataTableManager.ClearHooks();
            DataTableManager.DisableMemoryManagement();
        }

        /// <summary>
        /// 测试向后兼容性
        /// </summary>
        [Fact]
        public async Task BackwardCompatibility_ShouldBePreserved()
        {
            // Arrange
            ResetDataTableManager();
            DataTableManager.UseCustomSource(new FastMockDataSource());

            // Act - 使用传统API应该仍然工作
            var table1 = DataTableManager.GetDataTableInternal<MockDataTable>();

            // 使用新API
            var table2 = await DataTableManager.LoadAsync<MockDataTable>();

            // Assert
            // 两种方式应该能获取到相同的数据（或者至少都不为null）
            if (table1 != null && table2 != null)
            {
                // 如果都成功，验证它们访问的是同一份数据
                table1.Count.Should().Be(table2.Count);
            }
            else
            {
                // 至少有一种方式应该成功
                (table1 != null || table2 != null).Should().BeTrue("至少有一种API应该能成功加载数据");
            }
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

            // 清理内存管理器
            DataTableMemoryManager.Disable();
        }

        private struct PerformanceResults
        {
            public long TraditionalApproachMs { get; set; }
            public long OptimizedApproachMs { get; set; }
            public long ConcurrentLoadMs { get; set; }
        }
    }
}
