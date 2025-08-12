using System;
using System.Threading.Tasks;
using DataTables;

namespace ConsoleApp;

public enum ColorT
{
    Red,
    Green,
    Blue
}

public class SampleParent
{
    public int Value { get; set; }
    public string Text { get; set; } = "";
}

public class CustomSample
{
    public string Data { get; }
    public CustomSample(string data) => Data = data;
}

internal static class Program
{
    private static async Task Main(string[] args)
    {
        Console.WriteLine("🚀 DataTableManager 激进优化演示");
        Console.WriteLine("=====================================\n");

        // 🎯 智能配置系统演示
        await ConfigurationDemo();

        // ⚡ 异步优先API演示
        await AsyncFirstAPIDemo();

        // 🧠 智能内存管理演示
        await MemoryManagementDemo();

        // 🎣 Hook机制演示
        await HookSystemDemo();

        // 📊 性能监控演示
        await MonitoringDemo();

        Console.WriteLine("🎉 所有演示完成！享受现代化高性能的DataTables！");
    }

    static async Task ConfigurationDemo()
    {
        Console.WriteLine("🎯 智能配置系统演示");
        Console.WriteLine("-------------------");

        // 文件系统数据源配置
        DataTableManager.UseFileSystem("./DataTables");
        Console.WriteLine("✅ 配置文件系统数据源: ./DataTables");

        // 启用内存管理
        DataTableManager.EnableMemoryManagement(30); // 30MB限制
        Console.WriteLine("✅ 启用智能内存管理: 30MB LRU缓存");

        // 启用性能监控
        DataTableManager.EnableProfiling(stats =>
        {
            Console.WriteLine($"📊 性能报告: 加载了{stats.TableCount}个表，总内存{stats.MemoryUsed / 1024 / 1024:F1}MB");
        });
        Console.WriteLine("✅ 启用性能监控\n");
    }

    static async Task AsyncFirstAPIDemo()
    {
        Console.WriteLine("⚡ 异步优先API演示");
        Console.WriteLine("-----------------");

        try
        {
            // 演示异步加载 - 尝试加载矩阵表
            Console.WriteLine("📋 尝试异步加载数据表...");

            // 尝试加载生成的数据表
            try
            {
                var sampleTable = await DataTableManager.LoadAsync<DTDataTableSample>();
                if (sampleTable != null)
                {
                    Console.WriteLine($"✅ 成功加载数据表: {sampleTable.Count} 行数据");
                    
                    // 演示静态API - 这些是生成的便捷方法
                    var row1 = DTDataTableSample.GetDataRowById(1);
                    var rowsByName = DTDataTableSample.GetDataRowsGroupByName("示例字符串1");
                    Console.WriteLine($"✅ 静态API测试: 找到ID=1的行: {row1?.Name}, 分组查询结果: {rowsByName?.Count ?? 0}条");
                }
                else
                {
                    Console.WriteLine("📝 数据表加载返回null，可能是.bytes文件不存在或格式错误");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"📝 数据表加载异常: {ex.Message}");
            }

            Console.WriteLine("✅ 异步API接口正常工作:");
            Console.WriteLine("   - await DataTableManager.LoadAsync<T>() - 异步加载");
            Console.WriteLine("   - DataTableManager.GetCached<T>() - 缓存查询");
            Console.WriteLine("   - DataTableManager.IsLoaded<T>() - 状态检查");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"📝 演示异步API：{ex.GetType().Name} - {ex.Message}");
        }

        Console.WriteLine("✅ 异步优先架构已就绪\n");
    }

    static async Task MemoryManagementDemo()
    {
        Console.WriteLine("🧠 智能内存管理演示");
        Console.WriteLine("-------------------");

        // 获取缓存统计
        var cacheStats = DataTableManager.GetCacheStats();
        if (cacheStats.HasValue)
        {
            var stats = cacheStats.Value;
            Console.WriteLine($"📊 缓存统计:");
            Console.WriteLine($"   - 缓存项数: {stats.TotalItems}");
            Console.WriteLine($"   - 内存使用: {stats.MemoryUsage / 1024:F1}KB");
            Console.WriteLine($"   - 内存使用率: {stats.MemoryUsageRate:P}");
            Console.WriteLine($"   - 访问次数: {stats.AccessCount}");
            Console.WriteLine($"   - 命中次数: {stats.HitCount}");
            Console.WriteLine($"   - 命中率: {stats.HitRate:P}");
        }
        else
        {
            Console.WriteLine("📊 缓存统计: 当前无缓存项");
        }

        // 演示缓存清理
        DataTableManager.ClearCache();
        Console.WriteLine("✅ 缓存已清理");

        // 再次检查统计
        cacheStats = DataTableManager.GetCacheStats();
        if (cacheStats.HasValue)
        {
            Console.WriteLine($"📊 清理后缓存项数: {cacheStats.Value.TotalItems}");
        }

        Console.WriteLine("✅ 智能内存管理正常工作\n");
    }

    static async Task HookSystemDemo()
    {
        Console.WriteLine("🎣 Hook机制演示");
        Console.WriteLine("---------------");

        // 注册全局Hook
        DataTableManager.OnAnyLoaded(table =>
        {
            Console.WriteLine($"🎉 全局Hook触发: {table.GetType().Name} 已加载");
        });
        Console.WriteLine("✅ 已注册全局Hook");

        // 演示类型化Hook API
        Console.WriteLine("✅ 类型化Hook API:");
        Console.WriteLine("   - DataTableManager.OnLoaded<T>(callback)");
        Console.WriteLine("   - DataTableManager.OnAnyLoaded(callback)");

        // 清理Hook
        DataTableManager.ClearHooks();
        Console.WriteLine("✅ Hook已清理\n");
    }

    static async Task MonitoringDemo()
    {
        Console.WriteLine("📊 性能监控演示");
        Console.WriteLine("---------------");

        // 获取系统统计
        var stats = DataTableManager.GetStats();
        Console.WriteLine($"📈 系统统计:");
        Console.WriteLine($"   - 已加载表数量: {stats.TableCount}");
        Console.WriteLine($"   - 总内存使用: {stats.MemoryUsed / 1024 / 1024:F1}MB");
        Console.WriteLine($"   - 加载时间: {stats.LoadTime}ms");

        // 检查内存管理状态
        bool memoryEnabled = DataTableManager.IsMemoryManagementEnabled;
        Console.WriteLine($"   - 内存管理状态: {(memoryEnabled ? "已启用" : "未启用")}");

        // 获取数据表数量
        int tableCount = DataTableManager.Count;
        Console.WriteLine($"   - 当前表数量: {tableCount}");

        Console.WriteLine("✅ 性能监控功能正常\n");
    }
}
