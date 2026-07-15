using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataTables
{
    [Flags]
    public enum Priority
    {
        Critical = 1,
        Normal = 2,
        Lazy = 4,
        All = Critical | Normal | Lazy
    }

    public readonly struct CacheStats
    {
        public readonly int TotalItems;

        public readonly long EstimatedMemoryUsageBytes;
        public readonly long AccessCount;
        public readonly long HitCount;
        public readonly float HitRate;

        public readonly float EstimatedBudgetUsageRate;
        public readonly DateTime LastAccessed;

        public CacheStats(int totalItems, long estimatedMemoryUsageBytes, long accessCount, long hitCount, float estimatedBudgetUsageRate, DateTime lastAccessed)
        {
            TotalItems = totalItems;
            EstimatedMemoryUsageBytes = estimatedMemoryUsageBytes;
            AccessCount = accessCount;
            HitCount = hitCount;
            HitRate = accessCount > 0 ? (float)hitCount / accessCount : 0f;
            EstimatedBudgetUsageRate = estimatedBudgetUsageRate;
            LastAccessed = lastAccessed;
        }
    }

    public readonly struct LoadStats
    {
        public readonly long LoadTime;
        public readonly int TableCount;
        public readonly long MemoryUsed;
        public readonly int SuccessCount;
        public readonly int FailureCount;
        public readonly int CacheHitCount;
        public readonly int LoadedCount;
        public readonly int CanceledCount;
        public readonly int UnregisteredCount;

        public LoadStats(long loadTime, int tableCount, long memoryUsed)
            : this(loadTime, tableCount, memoryUsed, tableCount, 0)
        {
        }

        public LoadStats(long loadTime, int tableCount, long memoryUsed, int successCount, int failureCount)
            : this(loadTime, tableCount, memoryUsed, successCount, failureCount, 0, successCount, 0, tableCount == 0 ? 1 : 0)
        {
        }

        public LoadStats(long loadTime, int tableCount, long memoryUsed, int successCount, int failureCount, int cacheHitCount, int loadedCount, int canceledCount, int unregisteredCount)
        {
            LoadTime = loadTime;
            TableCount = tableCount;
            MemoryUsed = memoryUsed;
            SuccessCount = successCount;
            FailureCount = failureCount;
            CacheHitCount = cacheHitCount;
            LoadedCount = loadedCount;
            CanceledCount = canceledCount;
            UnregisteredCount = unregisteredCount;
        }
    }

    public readonly struct TableRegistration
    {
        private readonly Func<DataTableContext, CancellationToken, ValueTask<DataTableBase?>>? m_ContextLoadAsync;

        public TableRegistration(Type tableType, string name, Priority priority, Func<DataTableContext, CancellationToken, ValueTask<DataTableBase?>> loadAsync)
        {
            TableType = tableType ?? throw new ArgumentNullException(nameof(tableType));
            Name = name ?? string.Empty;
            Priority = priority;
            m_ContextLoadAsync = loadAsync ?? throw new ArgumentNullException(nameof(loadAsync));
        }

        public Type TableType { get; }
        public string Name { get; }
        public Priority Priority { get; }

        public ValueTask<DataTableBase?> LoadAsync(DataTableContext context, CancellationToken cancellationToken = default)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            return m_ContextLoadAsync(context, cancellationToken);
        }
    }

    public delegate void DataTableLoadedHook<T>(T dataTable) where T : DataTableBase;
    public delegate void DataTableLoadedHook(DataTableBase dataTable);
    public delegate void ProfilingHook(LoadStats stats);

    /// <summary>
    /// 默认数据表上下文的静态门面。单数据集应用可直接使用；需要隔离的数据集应实例化
    /// <see cref="DataTableContext"/>。
    /// </summary>
    public static class DataTableManager
    {
        private static readonly DataTableContext s_DefaultContext = new();
        private static readonly object s_DefaultInitializationGate = new();

        public static int Count => s_DefaultContext.Count;
        public static bool IsEstimatedMemoryBudgetEnabled => s_DefaultContext.IsEstimatedMemoryBudgetEnabled;

        public static DataTableParseExecution ParseExecution
        {
            get => s_DefaultContext.ParseExecution;
            set => s_DefaultContext.ParseExecution = value;
        }

        public static void UseFileSystem(string dataDirectory) => s_DefaultContext.UseFileSystem(dataDirectory);
        public static void UseNetwork(string baseUrl) => s_DefaultContext.UseNetwork(baseUrl);
        public static void UseDataSource(IDataSource source) => s_DefaultContext.UseDataSource(source);
        public static void UseCompositeSource(params IDataSource[] sources) => s_DefaultContext.UseCompositeSource(sources);
        public static void EnableEstimatedMemoryBudget(int maxEstimatedMemoryMB, Func<DataTableBase, long>? estimatedSizeProvider = null)
            => s_DefaultContext.EnableEstimatedMemoryBudget(maxEstimatedMemoryMB, estimatedSizeProvider);

        public static void DisableEstimatedMemoryBudget() => s_DefaultContext.DisableEstimatedMemoryBudget();

        public static void EnableProfiling(Action<LoadStats> onPerformanceReport) => s_DefaultContext.EnableProfiling(onPerformanceReport);
        public static void RegisterTables(IReadOnlyList<TableRegistration> registrations) => s_DefaultContext.RegisterTables(registrations);
        public static void ClearTableRegistrations() => s_DefaultContext.ClearTableRegistrations();

        public static void RegisterFactory<TTable, TRow, TFactory>()
            where TTable : DataTableBase
            where TRow : DataRowBase
            where TFactory : IDataTableFactory<TTable, TRow>, new()
            => DataTableFactoryManager.RegisterFactory<TTable, TRow, TFactory>();

        public static async ValueTask<LoadStats> PreheatAsync(Priority priority = Priority.Critical | Priority.Normal, CancellationToken cancellationToken = default)
        {
            EnsureDefaultContextInitialized();
            if (!s_DefaultContext.HasTableRegistrations)
            {
                s_DefaultContext.RegisterTables(GetGeneratedTableRegistrations());
            }
            return await s_DefaultContext.PreheatAsync(priority, cancellationToken);
        }

        public static ValueTask<LoadStats> PreloadAllAsync(CancellationToken cancellationToken = default)
            => PreheatAsync(Priority.All, cancellationToken);

        public static ValueTask<T?> LoadAsync<T>(string name = "", CancellationToken cancellationToken = default) where T : DataTableBase
        {
            EnsureDefaultContextInitialized();
            return s_DefaultContext.LoadAsync<T>(name, cancellationToken);
        }

        public static T? GetCached<T>(string name = "") where T : DataTableBase => s_DefaultContext.GetCached<T>(name);
        public static bool IsLoaded<T>(string name = "") where T : DataTableBase => s_DefaultContext.IsLoaded<T>(name);

        public static void OnLoaded<T>(Action<T> onLoaded) where T : DataTableBase => s_DefaultContext.OnLoaded(onLoaded);
        public static void OnAnyLoaded(Action<DataTableBase> onLoaded) => s_DefaultContext.OnAnyLoaded(onLoaded);
        public static void ClearHooks() => s_DefaultContext.ClearHooks();
        public static LoadStats GetStats() => s_DefaultContext.GetStats();
        public static CacheStats? GetCacheStats() => s_DefaultContext.GetCacheStats();
        public static void ClearCache() => s_DefaultContext.ClearCache();

        public static bool DestroyDataTable<T>(string name = "") where T : DataTableBase => s_DefaultContext.DestroyDataTable<T>(name);
        private static void EnsureDefaultContextInitialized()
        {
            if (s_DefaultContext.HasDataSource) return;
            lock (s_DefaultInitializationGate)
            {
                if (!s_DefaultContext.HasDataSource)
                {
                    s_DefaultContext.UseFileSystem(DetectDataDirectory());
                }
            }
        }

        private static string DetectDataDirectory()
        {
            var candidates = new[]
            {
                "./DataTables",
                "./Data",
                "./Datas",
                "./Resources/DataTables",
                "../DataTables",
                "../../DataTables"
            };
            foreach (var candidate in candidates)
            {
                if (Directory.Exists(candidate)) return candidate;
            }
            var defaultPath = "./DataTables";
            Directory.CreateDirectory(defaultPath);
            return defaultPath;
        }

        private static IReadOnlyList<TableRegistration> GetGeneratedTableRegistrations()
        {
            try
            {
                return AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(assembly =>
                    {
                        try { return assembly.GetTypes(); }
                        catch { return Array.Empty<Type>(); }
                    })
                    .Where(type => type.Name == "DataTableManagerExtension")
                    .Select(type => type.GetProperty("TableRegistrations")?.GetValue(null) as IReadOnlyList<TableRegistration>)
                    .Where(registrations => registrations != null)
                    .SelectMany(registrations => registrations!)
                    .GroupBy(GetRegistrationKey, StringComparer.Ordinal)
                    .Select(group => group.Last())
                    .ToArray();
            }
            catch
            {
                return Array.Empty<TableRegistration>();
            }
        }

        private static string GetRegistrationKey(TableRegistration registration)
            => (registration.TableType.AssemblyQualifiedName ?? registration.TableType.FullName ?? registration.TableType.Name) + "|" + registration.Name;
    }
}
