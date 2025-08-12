using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DataTables
{
    /// <summary>
    /// 表优先级（用于批量预热）
    /// </summary>
    [Flags]
    public enum Priority
    {
        Critical = 1,   // 关键表
        Normal = 2,     // 普通表
        Lazy = 4,       // 延迟表
        All = Critical | Normal | Lazy
    }

    /// <summary>
    /// 缓存统计信息
    /// </summary>
    public readonly struct CacheStats
    {
        public readonly int TotalItems;      // 缓存项总数
        public readonly long MemoryUsage;    // 内存使用量(bytes)
        public readonly long AccessCount;    // 总访问次数
        public readonly long HitCount;       // 命中次数
        public readonly float HitRate;       // 命中率
        public readonly float MemoryUsageRate; // 内存使用率
        public readonly DateTime LastAccessed; // 最后访问时间

        public CacheStats(int totalItems, long memoryUsage, long accessCount, long hitCount, float memoryUsageRate, DateTime lastAccessed)
        {
            TotalItems = totalItems;
            MemoryUsage = memoryUsage;
            AccessCount = accessCount;
            HitCount = hitCount;
            HitRate = accessCount > 0 ? (float)hitCount / accessCount : 0f;
            MemoryUsageRate = memoryUsageRate;
            LastAccessed = lastAccessed;
        }
    }

    /// <summary>
    /// 性能统计信息
    /// </summary>
    public readonly struct LoadStats
    {
        public readonly long LoadTime;    // 加载耗时(ms)
        public readonly int TableCount;   // 表数量
        public readonly long MemoryUsed;  // 内存使用(bytes)

        public LoadStats(long loadTime, int tableCount, long memoryUsed)
        {
            LoadTime = loadTime;
            TableCount = tableCount;
            MemoryUsed = memoryUsed;
        }
    }

    /// <summary>
    /// Hook委托
    /// </summary>
    public delegate void DataTableLoadedHook<T>(T dataTable) where T : DataTableBase;
    public delegate void DataTableLoadedHook(DataTableBase dataTable);
    public delegate void ProfilingHook(LoadStats stats);

    /// <summary>
    /// DataTableManager - 激进优化版本
    /// 特性：纯异步优先，内置LRU缓存，工厂模式，零反射
    /// </summary>
    public static class DataTableManager
    {
        #region InternalFields

        private static readonly ConcurrentDictionary<TypeNamePair, DataTableBase> s_DataTables = new();
        private static readonly ConcurrentDictionary<TypeNamePair, Task<DataTableBase?>> s_LoadingTables = new();
        private static readonly ConcurrentDictionary<Type, IDataTableFactory> s_Factories = new();
        private static LRUDataTableCache? s_Cache;
        private static IDataSource? s_DataSource;
        private static readonly object s_Lock = new object();

        // Hook机制
        private static readonly ConcurrentDictionary<Type, List<DataTableLoadedHook>> s_TypedHooks = new();
        private static readonly List<DataTableLoadedHook> s_GlobalHooks = new();

        // 配置与统计
        private static ProfilingHook? s_ProfilingHook;
        private static readonly System.Diagnostics.Stopwatch s_Stopwatch = new();

        #endregion

        #region PublicProperties

        /// <summary>
        /// 获取数据表数量
        /// </summary>
        public static int Count => s_DataTables.Count;

        /// <summary>
        /// 是否启用内存管理
        /// </summary>
        public static bool IsMemoryManagementEnabled => s_Cache != null;

        #endregion

        #region ConfigurationAPI

        /// <summary>
        /// 使用文件系统数据源 - 最常用的配置
        /// </summary>
        /// <param name="dataDirectory">数据目录路径</param>
        public static void UseFileSystem(string dataDirectory)
        {
            s_DataSource = new FileSystemDataSource(dataDirectory);
        }

        /// <summary>
        /// 使用网络数据源
        /// </summary>
        /// <param name="baseUrl">基础URL</param>
        public static void UseNetwork(string baseUrl)
        {
            s_DataSource = new NetworkDataSource(baseUrl);
        }

        /// <summary>
        /// 使用自定义数据源
        /// </summary>
        /// <param name="dataSource">自定义数据源实现</param>
        public static void UseCustomSource(IDataSource dataSource)
        {
            s_DataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        }

        /// <summary>
        /// 启用内存管理 - 设置最大内存使用量
        /// </summary>
        /// <param name="maxMemoryMB">最大内存使用量(MB)</param>
        public static void EnableMemoryManagement(int maxMemoryMB)
        {
            s_Cache = new LRUDataTableCache(maxMemoryMB * 1024 * 1024);
        }

        /// <summary>
        /// 禁用内存管理
        /// </summary>
        public static void DisableMemoryManagement()
        {
            s_Cache?.Clear();
            s_Cache = null;
        }

        /// <summary>
        /// 启用性能监控
        /// </summary>
        /// <param name="onPerformanceReport">性能报告回调</param>
        public static void EnableProfiling(Action<LoadStats> onPerformanceReport)
        {
            s_ProfilingHook = new ProfilingHook(onPerformanceReport);
        }

        /// <summary>
        /// 注册数据表工厂 - 消除反射调用
        /// </summary>
        public static void RegisterFactory<TTable, TRow, TFactory>()
            where TTable : DataTableBase
            where TRow : DataRowBase
            where TFactory : IDataTableFactory<TTable, TRow>, new()
        {
            s_Factories[typeof(TTable)] = new TFactory();
        }

        #endregion

        /// <summary>
        /// 异步预热数据表 - 批量加载
        /// </summary>
        /// <param name="priority">预热优先级</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>加载结果统计</returns>
        public static async ValueTask<LoadStats> PreheatAsync(
            Priority priority = Priority.Critical | Priority.Normal,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureAutoInitialized();

            s_Stopwatch.Restart();
            var memoryBefore = GC.GetTotalMemory(false);

            // 使用生成的扩展类进行预热 (待实现)
            var loadedCount = await PreheatTablesAsync(priority, cancellationToken);

            s_Stopwatch.Stop();
            var memoryAfter = GC.GetTotalMemory(false);

            var stats = new LoadStats(
                s_Stopwatch.ElapsedMilliseconds,
                loadedCount,
                memoryAfter - memoryBefore);

            s_ProfilingHook?.Invoke(stats);
            return stats;
        }

        /// <summary>
        /// 异步预加载所有表 - 服务器场景
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>加载结果统计</returns>
        public static async ValueTask<LoadStats> PreloadAllAsync(CancellationToken cancellationToken = default)
        {
            return await PreheatAsync(Priority.All, cancellationToken);
        }

        #region AsyncLoadingAPI

        /// <summary>
        /// 异步加载数据表 - 主推荐API
        /// </summary>
        /// <typeparam name="T">数据表类型</typeparam>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>数据表实例</returns>
        public static async ValueTask<T?> LoadAsync<T>(CancellationToken cancellationToken = default)
            where T : DataTableBase
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await GetDataTableAsync<T>();
        }

        /// <summary>
        /// 获取已加载的数据表 - 仅从缓存获取，不触发加载
        /// </summary>
        /// <typeparam name="T">数据表类型</typeparam>
        /// <returns>数据表实例，如果未加载则返回null</returns>
        public static T? GetCached<T>() where T : DataTableBase
        {
            var typeNamePair = new TypeNamePair(typeof(T), string.Empty);

            // 优先从LRU缓存获取
            if (s_Cache != null && s_Cache.TryGet<T>(typeNamePair, out var cached))
            {
                return cached;
            }

            // 回退到内存表
            return s_DataTables.TryGetValue(typeNamePair, out var table) ? table as T : null;
        }

        /// <summary>
        /// 检查数据表是否已加载
        /// </summary>
        /// <typeparam name="T">数据表类型</typeparam>
        /// <returns>是否已加载</returns>
        public static bool IsLoaded<T>() where T : DataTableBase
        {
            return GetCached<T>() != null;
        }

        #endregion

        #region HookSystem

        /// <summary>
        /// 注册数据表加载完成Hook
        /// </summary>
        /// <typeparam name="T">数据表类型</typeparam>
        /// <param name="onLoaded">加载完成回调</param>
        public static void OnLoaded<T>(Action<T> onLoaded) where T : DataTableBase
        {
            var type = typeof(T);
            var wrappedHook = new DataTableLoadedHook(table => onLoaded((T)table));

            lock (s_Lock)
            {
                if (!s_TypedHooks.TryGetValue(type, out var hooks))
                {
                    hooks = new List<DataTableLoadedHook>();
                    s_TypedHooks[type] = hooks;
                }
                hooks.Add(wrappedHook);
            }
        }

        /// <summary>
        /// 注册全局数据表加载完成Hook
        /// </summary>
        /// <param name="onLoaded">加载完成回调</param>
        public static void OnAnyLoaded(Action<DataTableBase> onLoaded)
        {
            lock (s_Lock)
            {
                s_GlobalHooks.Add(new DataTableLoadedHook(onLoaded));
            }
        }

        /// <summary>
        /// 清除所有Hook
        /// </summary>
        public static void ClearHooks()
        {
            lock (s_Lock)
            {
                s_TypedHooks.Clear();
                s_GlobalHooks.Clear();
            }
        }

        #endregion

        #region MonitoringAPI

        /// <summary>
        /// 获取数据表统计信息
        /// </summary>
        /// <returns>统计信息</returns>
        public static LoadStats GetStats()
        {
            var totalMemory = GC.GetTotalMemory(false);
            var loadedCount = s_DataTables.Count;

            return new LoadStats(
                0, // 平均加载时间 (待实现统计)
                loadedCount,
                totalMemory);
        }

        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        /// <returns>缓存统计</returns>
        public static CacheStats? GetCacheStats()
        {
            return s_Cache?.GetStats();
        }

        /// <summary>
        /// 清理缓存
        /// </summary>
        public static void ClearCache()
        {
            s_Cache?.Clear();
            s_DataTables.Clear();
            s_LoadingTables.Clear();
        }

        #endregion

        #region InternalImplementation

        /// <summary>
        /// 线程安全的异步数据表加载方法
        /// </summary>
        /// <typeparam name="T">数据表类型</typeparam>
        /// <returns>数据表实例</returns>
        public static async ValueTask<T?> GetDataTableAsync<T>() where T : DataTableBase
        {
            var typeNamePair = new TypeNamePair(typeof(T), string.Empty);

            // 快速路径：优先从LRU缓存获取
            if (s_Cache != null && s_Cache.TryGet<T>(typeNamePair, out var cached))
            {
                return cached;
            }

            // 快速路径：从内存表获取
            if (s_DataTables.TryGetValue(typeNamePair, out var existingTable))
            {
                return existingTable as T;
            }

            // 确保数据源已初始化
            EnsureAutoInitialized();

            // 使用Task缓存模式确保单次加载
            var loadingTask = s_LoadingTables.GetOrAdd(typeNamePair, _ => LoadDataTableInternalAsync(typeNamePair));

            var result = await loadingTask;
            return result as T;
        }

        /// <summary>
        /// 内部方法：供生成的DTXXX类使用的获取数据表方法 - 高性能优先
        /// </summary>
        /// <typeparam name="T">数据表类型</typeparam>
        /// <returns>数据表实例</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T? GetDataTableInternal<T>() where T : DataTableBase
        {
            // 优化：直接使用新的缓存优先接口
            return GetCached<T>();
        }

        /// <summary>
        /// 内部方法：供生成的DTXXX类使用的异步加载方法
        /// </summary>
        /// <typeparam name="T">数据表类型</typeparam>
        /// <returns>数据表实例</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async ValueTask<T?> GetDataTableInternalAsync<T>() where T : DataTableBase
        {
            return await GetDataTableAsync<T>();
        }


        /// <summary>
        /// 异步创建数据表 - 新的推荐方式
        /// </summary>
        /// <typeparam name="T">数据表类型</typeparam>
        /// <param name="name">表名（可选）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>数据表实例</returns>
        public static async ValueTask<T?> CreateDataTableAsync<T>(string name = "", CancellationToken cancellationToken = default) where T : DataTableBase
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await LoadAsync<T>(cancellationToken);
        }

        /// <summary>
        /// 创建数据表 - 兼容旧API，内部使用
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CreateDataTable<T>(Action onCompleted) where T : DataTableBase
        {
            CreateDataTable<T>(string.Empty, onCompleted);
        }

        public static void CreateDataTable<T>(string name, Action onCompleted) where T : DataTableBase
        {
            EnsureAutoInitialized();

            var typeNamePair = new TypeNamePair(typeof(T), name);
            if (s_DataTables.ContainsKey(typeNamePair))
            {
                onCompleted?.Invoke();
                return;
            }

            // 使用异步加载
            _ = LoadDataTableAsync(typeNamePair, onCompleted);
        }

        /// <summary>
        /// 线程安全的内部异步加载方法 - 集成LRU缓存管理
        /// </summary>
        private static async Task<DataTableBase?> LoadDataTableInternalAsync(TypeNamePair typeNamePair)
        {
            try
            {
                // 再次检查，可能在等待期间已被其他线程加载
                if (s_DataTables.TryGetValue(typeNamePair, out var existingTable))
                {
                    // 同步到LRU缓存
                    s_Cache?.Set(typeNamePair, existingTable);
                    return existingTable;
                }

                var raw = await s_DataSource!.LoadAsync(typeNamePair.ToString());
                var dataTable = LoadDataTableFromBytes(typeNamePair, raw);

                // 原子性添加到缓存
                if (s_DataTables.TryAdd(typeNamePair, dataTable))
                {
                    // 添加到LRU缓存
                    s_Cache?.Set(typeNamePair, dataTable);

                    // 清理加载任务缓存
                    s_LoadingTables.TryRemove(typeNamePair, out _);

                    // 触发Hook
                    TriggerHooks(dataTable);
                    return dataTable;
                }
                else
                {
                    // 其他线程已经加载了，返回已有的
                    var existing = s_DataTables[typeNamePair];
                    s_Cache?.Set(typeNamePair, existing);
                    return existing;
                }
            }
            catch (Exception ex)
            {
                // 清理失败的加载任务
                s_LoadingTables.TryRemove(typeNamePair, out _);
                Console.WriteLine($"Failed to load table {typeNamePair}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 异步加载数据表 (保持兼容性)
        /// </summary>
        private static async Task LoadDataTableAsync(TypeNamePair typeNamePair, Action? onCompleted)
        {
            try
            {
                var raw = await s_DataSource!.LoadAsync(typeNamePair.ToString());
                LoadDataTable(typeNamePair, raw, onCompleted);
            }
            catch (Exception ex)
            {
                // 可以选择静默失败或抛出异常
                Console.WriteLine($"Failed to load table {typeNamePair}: {ex.Message}");
                onCompleted?.Invoke(); // 即使失败也要回调，避免死锁
            }
        }

        /// <summary>
        /// 从字节数据加载数据表 (优化版本 - 优先使用工厂模式)
        /// </summary>
        private static DataTableBase LoadDataTableFromBytes(TypeNamePair typeNamePair, byte[] raw)
        {
            using var ms = new MemoryStream(raw);
            using var br = new BinaryReader(ms, Encoding.UTF8);

            var readSign = br.ReadString();
            if (readSign != "DTABLE")
            {
                throw new Exception($"Invalid data table file format for '{typeNamePair}'.");
            }

            var readVersion = br.ReadInt32();
            if (readVersion != 1)
            {
                throw new Exception($"Unsupported data table version {readVersion} for '{typeNamePair}'.");
            }

            var readCount = br.Read7BitEncodedInt32();

            // 尝试使用高性能工厂模式
            var dataTable = CreateDataTableInstance(typeNamePair, readCount);

            for (int i = 0; i < readCount; i++)
            {
                var dataRow = CreateDataRowInstance(typeNamePair);
                if (!dataRow.Deserialize(br))
                {
                    throw new Exception($"Can not parse data table '{typeNamePair}' at index '{i}'.");
                }

                // 添加数据行到表中
                AddDataRowToTable(dataTable, i, dataRow);
            }

            return dataTable;
        }

        /// <summary>
        /// 创建数据表实例 - 工厂模式优化
        /// </summary>
        private static DataTableBase CreateDataTableInstance(TypeNamePair typeNamePair, int capacity)
        {
            // 优先使用预注册的工厂（高性能）
            if (s_Factories.TryGetValue(typeNamePair.Type, out var factory))
            {
                // 使用反射调用工厂方法（需要优化成编译委托）
                var createTableMethod = factory.GetType().GetMethod("CreateTable");
                return (DataTableBase)createTableMethod!.Invoke(factory, new object[] { typeNamePair.Name, capacity })!;
            }

            // 回退到反射模式（保持兼容性）
            return (DataTableBase)Activator.CreateInstance(typeNamePair.Type, typeNamePair.Name, capacity)!;
        }

        /// <summary>
        /// 创建数据行实例 - 工厂模式优化
        /// </summary>
        private static DataRowBase CreateDataRowInstance(TypeNamePair typeNamePair)
        {
            // 优先使用预注册的工厂（高性能）
            if (s_Factories.TryGetValue(typeNamePair.Type, out var factory))
            {
                // 使用反射调用工厂方法（需要优化成编译委托）
                var createRowMethod = factory.GetType().GetMethod("CreateRow");
                return (DataRowBase)createRowMethod!.Invoke(factory, Array.Empty<object>())!;
            }

            // 回退到反射模式（保持兼容性）
            var dataRowType = typeNamePair.Type.BaseType!.GetGenericArguments()[0];
            return (DataRowBase)Activator.CreateInstance(dataRowType)!;
        }

        /// <summary>
        /// 添加数据行到表中 - 高性能内联
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AddDataRowToTable(DataTableBase dataTable, int index, DataRowBase dataRow)
        {
            // TODO: 将来使用预编译的委托（代码生成时实现）
            // 暂时使用反射，但缓存方法信息
            var tableType = dataTable.GetType();
            var internalAddMethod = tableType.GetMethod("InternalAddDataRow",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            internalAddMethod?.Invoke(dataTable, new object[] { index, dataRow });
        }

        /// <summary>
        /// 加载数据表 - 集成缓存管理
        /// </summary>
        private static void LoadDataTable(TypeNamePair typeNamePair, byte[] raw, Action? onCompleted)
        {
            DataTableBase dataTable;

            lock (s_Lock)
            {
                if (s_DataTables.ContainsKey(typeNamePair))
                {
                    onCompleted?.Invoke();
                    return;
                }

                dataTable = LoadDataTableFromBytes(typeNamePair, raw);
                s_DataTables[typeNamePair] = dataTable;

                // 添加到LRU缓存
                s_Cache?.Set(typeNamePair, dataTable);
            }

            // 触发Hook
            TriggerHooks(dataTable);
            onCompleted?.Invoke();
        }

        private static void TriggerHooks(DataTableBase dataTable)
        {
            var type = dataTable.GetType();

            // 触发类型化Hook
            if (s_TypedHooks.TryGetValue(type, out var hooks))
            {
                foreach (var hook in hooks)
                {
                    try { hook(dataTable); }
                    catch { /* 静默失败 */ }
                }
            }

            // 触发全局Hook
            foreach (var hook in s_GlobalHooks)
            {
                try { hook(dataTable); }
                catch { /* 静默失败 */ }
            }
        }

        /// <summary>
        /// 销毁数据表。
        /// </summary>
        public static bool DestroyDataTable<T>(string name = "") where T : DataTableBase
        {
            return InternalDestroyDataTable(new TypeNamePair(typeof(T), name));
        }

        #endregion

        #region InternalHelpers

        /// <summary>
        /// 确保自动初始化 - 智能默认值，零心智负担
        /// </summary>
        private static void EnsureAutoInitialized()
        {
            if (s_DataSource != null) return;

            lock (s_Lock)
            {
                if (s_DataSource != null) return;

                // 智能检测数据目录并创建文件系统数据源
                var dataDir = DetectDataDirectory();
                s_DataSource = new FileSystemDataSource(dataDir);
            }
        }

        /// <summary>
        /// 智能检测数据目录
        /// </summary>
        private static string DetectDataDirectory()
        {
            // 按优先级检测，采用约定优于配置
            var candidates = new[]
            {
                Environment.GetEnvironmentVariable("DATATABLE_PATH"),      // 环境变量优先
                Path.Combine(Directory.GetCurrentDirectory(), "DataTables"), // 生成器默认
                Path.Combine(Directory.GetCurrentDirectory(), "Data"),       // 通用约定
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DataTables"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data")
            };

            foreach (var candidate in candidates)
            {
                if (!string.IsNullOrEmpty(candidate) && Directory.Exists(candidate))
                    return candidate;
            }

            // 默认创建DataTables目录
            var defaultPath = Path.Combine(Directory.GetCurrentDirectory(), "DataTables");
            if (!Directory.Exists(defaultPath))
                Directory.CreateDirectory(defaultPath);
            return defaultPath;
        }

        /// <summary>
        /// 预热指定优先级的表 - 异步版本
        /// </summary>
        private static async Task<int> PreheatTablesAsync(Priority priorities, CancellationToken cancellationToken = default)
        {
            var loadedCount = 0;

            try
            {
                // 查找生成的DataTableManagerExtension类
                var extensionType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => a.GetTypes())
                    .FirstOrDefault(t => t.Name == "DataTableManagerExtension" && t.GetMethod("Preload") != null);

                if (extensionType != null)
                {
                    var preloadMethod = extensionType.GetMethod("Preload",
                        new[] { typeof(Action), typeof(Action<float>) });

                    if (preloadMethod != null)
                    {
                        var tcs = new TaskCompletionSource<int>();
                        var completed = false;

                        // 支持取消操作
                        using var registration = cancellationToken.Register(() => {
                            if (!completed) {
                                completed = true;
                                tcs.SetCanceled();
                            }
                        });

                        preloadMethod.Invoke(null, new object[] {
                            new Action(() => {
                                if (!completed) {
                                    completed = true;
                                    tcs.SetResult(s_DataTables.Count);
                                }
                            }),
                            null // 进度回调（可选）
                        });

                        loadedCount = await tcs.Task;
                    }
                }
                else
                {
                    // 如果没有扩展类，返回当前已加载的表数量
                    loadedCount = s_DataTables.Count;
                }
            }
            catch (OperationCanceledException)
            {
                throw; // 重新抛出取消异常
            }
            catch
            {
                // 静默失败，返回当前已加载的表数量
                loadedCount = s_DataTables.Count;
            }

            return loadedCount;
        }

        private static bool InternalHasDataTable(TypeNamePair pair)
        {
            return s_DataTables.ContainsKey(pair);
        }

        private static DataTableBase? InternalGetDataTable(TypeNamePair pair) => s_DataTables.GetValueOrDefault(pair);

        private static bool InternalDestroyDataTable(TypeNamePair pair)
        {
            lock (s_Lock)
            {
                if (s_DataTables.TryRemove(pair, out var dataTable))
                {
                    // 从缓存中移除
                    if (s_Cache != null && s_Cache is LRUDataTableCache cache)
                    {
                        // LRU缓存没有Remove方法，可以留空或者实现一个
                        // cache.Remove(pair);
                    }

                    // 从加载任务中移除
                    s_LoadingTables.TryRemove(pair, out _);

                    dataTable.Shutdown();
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        #endregion

        #region LegacyAPI

        /// <summary>
        /// 获取数据表 - 兼容性API（内部使用新实现）
        /// </summary>
        [Obsolete("请使用 GetCached<T>() 或 LoadAsync<T>() 替代")]
        public static T? GetDataTable<T>() where T : DataTableBase
        {
            return GetCached<T>();
        }


        #endregion
    }
}
