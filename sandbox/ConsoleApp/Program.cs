using System;
using System.IO;
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
    private static async Task<int> Main(string[] args)
    {
        Console.WriteLine("🚀 DataTableManager 激进优化演示");
        Console.WriteLine("=====================================\n");

        // 🎯 智能配置系统演示
        await ConfigurationDemo();

        // ⚡ 异步优先API演示
        var asyncFirstSucceeded = await AsyncFirstAPIDemo();

        // 🧩 ColumnTable 示例演示
        var columnTableSucceeded = await ColumnTableDemo();

        // 🧠 智能内存管理演示
        await MemoryManagementDemo();

        // 🎣 Hook机制演示
        await HookSystemDemo();

        // 📊 性能监控演示
        await MonitoringDemo();

        if (!asyncFirstSucceeded || !columnTableSucceeded)
        {
            Console.Error.WriteLine("❌ 示例数据加载失败；请重新生成 Generated 与 DataTables 目录中的产物。");
            return 1;
        }

        Console.WriteLine("🎉 所有演示完成！享受现代化高性能的DataTables！");
        return 0;
    }

    static async Task ConfigurationDemo()
    {
        Console.WriteLine("🎯 智能配置系统演示");
        Console.WriteLine("-------------------");

        // 文件系统数据源配置
        var dataDirectory = Path.Combine(AppContext.BaseDirectory, "DataTables");
        DataTableManager.UseFileSystem(dataDirectory);
        Console.WriteLine($"✅ 配置文件系统数据源: {dataDirectory}");

        // 启用内存管理
        DataTableManager.EnableEstimatedMemoryBudget(30); // 30MB估算预算
        Console.WriteLine("✅ 启用估算内存预算: 30MB LRU缓存");

        // 启用性能监控
        DataTableManager.EnableProfiling(stats =>
        {
            Console.WriteLine($"📊 性能报告: 加载了{stats.TableCount}个表，总内存{stats.MemoryUsed / 1024 / 1024:F1}MB");
        });
        Console.WriteLine("✅ 启用性能监控\n");
    }

    static async Task<bool> AsyncFirstAPIDemo()
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
                var dataDirectory = Path.Combine(AppContext.BaseDirectory, "DataTables");
                using var context = new DataTableContext(new FileSystemDataSource(dataDirectory));

                // 生成查询显式绑定 context；同一进程可安全加载多套数据。
                await DataTableManagerExtension.PreloadAsync(context);

                var sampleTable = context.GetCached<DTDataTableSample>();
                if (sampleTable != null)
                {
                    Console.WriteLine($"✅ 成功加载数据表: {sampleTable.Count} 行数据");

                    // 演示生成的静态查询 API
                    const string sampleName = "示例字符k串1";
                    var row1 = DTDataTableSample.GetById(context, 1);
                    var rowsByName = DTDataTableSample.GetManyByName(context, sampleName);
                    if (row1?.Name != sampleName || rowsByName?.Count != 1)
                    {
                        Console.Error.WriteLine("❌ 静态查询验证失败：示例数据与生成代码不匹配。");
                        return false;
                    }

                    var split1 = context.GetCached<DTDataTableSplitSample>("x001");
                    var split2 = context.GetCached<DTDataTableSplitSample>("x002");
                    var splitRow1 = DTDataTableSplitSample.GetById(context, "x001", 1);
                    var splitRow2 = DTDataTableSplitSample.GetById(context, "x002", 1);
                    if (split1?.Name != "x001" || split2?.Name != "x002"
                        || ReferenceEquals(split1, split2) || splitRow1 is null || splitRow2 is null)
                    {
                        Console.Error.WriteLine("❌ 分表查询验证失败：静态查询没有使用指定的 child name。");
                        return false;
                    }

                    Console.WriteLine($"✅ 静态API测试: 找到ID=1的行: {row1?.Name}, 分组查询结果: {rowsByName?.Count ?? 0}条");
                    Console.WriteLine("✅ 分表查询测试: x001/x002 使用各自缓存实例");
                    Console.WriteLine("✅ 异步优先架构已就绪\n");
                    return true;
                }

                Console.WriteLine("📝 数据表加载返回null，可能是.bytes文件不存在或格式错误");
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

        return false;
    }

    static async Task<bool> ColumnTableDemo()
    {
        Console.WriteLine("🧩 ColumnTable 示例演示");
        Console.WriteLine("---------------------");

        try
        {
            // 准备一个简单的 ColumnTable 的 .bytes（已放在 DataTables 目录后可直接加载）
            // 这里演示按名称加载失败容错
            var table = await DataTableManager.LoadAsync<DTColumnTableSample>();
            if (table != null)
            {
                Console.WriteLine($"✅ 载入示例数据: 当前已有 {table.Count} 行");
                Console.WriteLine();
                return true;
            }

            Console.WriteLine("ℹ️ 未检测到示例 .bytes，可在工程中添加 Column 布局的示例后重试");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"📝 ColumnTable 演示异常: {ex.Message}");
        }

        Console.WriteLine();
        return false;
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
            Console.WriteLine($"   - 估算内存使用: {stats.EstimatedMemoryUsageBytes / 1024:F1}KB");
            Console.WriteLine($"   - 估算预算使用率: {stats.EstimatedBudgetUsageRate:P}");
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
        bool memoryEnabled = DataTableManager.IsEstimatedMemoryBudgetEnabled;
        Console.WriteLine($"   - 估算内存预算状态: {(memoryEnabled ? "已启用" : "未启用")}");

        // 获取数据表数量
        int tableCount = DataTableManager.Count;
        Console.WriteLine($"   - 当前表数量: {tableCount}");

        Console.WriteLine("✅ 性能监控功能正常\n");
    }
}
