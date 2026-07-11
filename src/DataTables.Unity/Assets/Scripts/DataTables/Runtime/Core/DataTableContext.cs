using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataTables
{
    /// <summary>
    /// 独立的数据表运行时上下文。每个实例拥有自己的数据源、加载任务、缓存、生命周期和 Hook。
    /// </summary>
    public sealed class DataTableContext : IDataTableManager, IDisposable
    {
        private readonly ConcurrentDictionary<TypeNamePair, DataTableBase> m_DataTables = new();
        private readonly ConcurrentDictionary<TypeNamePair, TaskCompletionSource<DataTableBase?>> m_LoadingTables = new();
        private readonly ConcurrentDictionary<TypeNamePair, long> m_TableGenerations = new();
        private readonly ConcurrentDictionary<Type, List<DataTableLoadedHook>> m_TypedHooks = new();
        private readonly ConcurrentDictionary<string, TableRegistration> m_TableRegistrations = new(StringComparer.Ordinal);
        private readonly List<DataTableLoadedHook> m_GlobalHooks = new();
        private readonly object m_Gate = new();
        private LRUDataTableCache? m_Cache;
        private IDataSource? m_DataSource;
        private long m_LifecycleGeneration;
        private long m_TotalLoadTimeMs;
        private long m_TotalMemoryDeltaBytes;
        private int m_TotalLoadSuccessCount;
        private int m_TotalLoadFailureCount;
        private int m_Disposed;
        private ProfilingHook? m_ProfilingHook;

        public DataTableContext()
        {
        }

        public DataTableContext(IDataSource dataSource)
        {
            m_DataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        }

        public int Count
        {
            get
            {
                ThrowIfDisposed();
                lock (m_Gate)
                {
                    return m_Cache?.GetStats().TotalItems ?? m_DataTables.Count;
                }
            }
        }

        public bool IsMemoryManagementEnabled
        {
            get
            {
                ThrowIfDisposed();
                lock (m_Gate)
                {
                    return m_Cache != null;
                }
            }
        }

        internal bool HasTableRegistrations => !m_TableRegistrations.IsEmpty;

        internal bool HasDataSource
        {
            get
            {
                lock (m_Gate)
                {
                    return m_DataSource != null;
                }
            }
        }

        public void UseFileSystem(string dataDirectory) => UseDataSource(new FileSystemDataSource(dataDirectory));

        public void UseNetwork(string baseUrl) => UseDataSource(new NetworkDataSource(baseUrl));

        public void UseCompositeSource(params IDataSource[] sources) => UseDataSource(new FallbackDataSource(sources));

        public void UseDataSource(IDataSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            ThrowIfDisposed();
            var tables = ResetState(source);
            ShutdownTables(tables);
        }

        public void EnableMemoryManagement(int maxMemoryMB)
        {
            if (maxMemoryMB <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxMemoryMB), "Memory limit must be greater than zero.");
            }

            ThrowIfDisposed();
            var cache = new LRUDataTableCache((long)maxMemoryMB * 1024 * 1024);
            lock (m_Gate)
            {
                var tables = m_Cache != null
                    ? m_Cache.Drain()
                    : m_DataTables.Select(pair => new KeyValuePair<TypeNamePair, DataTableBase>(pair.Key, pair.Value)).ToArray();
                m_DataTables.Clear();
                m_Cache = cache;
                foreach (var table in tables)
                {
                    cache.Set(table.Key, table.Value);
                }
            }
        }

        public void DisableMemoryManagement()
        {
            ThrowIfDisposed();
            lock (m_Gate)
            {
                if (m_Cache == null) return;
                var tables = m_Cache.Drain();
                m_Cache = null;
                foreach (var table in tables)
                {
                    m_DataTables[table.Key] = table.Value;
                }
            }
        }

        public void EnableProfiling(Action<LoadStats> onPerformanceReport)
        {
            if (onPerformanceReport == null) throw new ArgumentNullException(nameof(onPerformanceReport));
            ThrowIfDisposed();
            m_ProfilingHook = new ProfilingHook(onPerformanceReport);
        }

        public void RegisterTables(IReadOnlyList<TableRegistration> registrations)
        {
            if (registrations == null) throw new ArgumentNullException(nameof(registrations));
            ThrowIfDisposed();
            foreach (var registration in registrations)
            {
                m_TableRegistrations[GetRegistrationKey(registration)] = registration;
            }
        }

        public void ClearTableRegistrations()
        {
            ThrowIfDisposed();
            m_TableRegistrations.Clear();
        }

        public async ValueTask<LoadStats> PreheatAsync(Priority priorities = Priority.Critical | Priority.Normal, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            var memoryBefore = GC.GetTotalMemory(false);
            var registrations = m_TableRegistrations.Values
                .Where(registration => (priorities & registration.Priority) != 0)
                .ToArray();

            if (registrations.Length == 0)
            {
                return new LoadStats(0, 0, 0, 0, 0, 0, 0, 0, 1);
            }

            var tasks = registrations.Select(async registration =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (HasDataTable(registration.TableType, registration.Name)) return (CacheHit: true, Loaded: false, Canceled: false);
                try
                {
                    var table = await registration.LoadAsync(this, cancellationToken);
                    return (CacheHit: false, Loaded: table != null, Canceled: false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return (CacheHit: false, Loaded: false, Canceled: true);
                }
                catch
                {
                    return (CacheHit: false, Loaded: false, Canceled: false);
                }
            });
            var results = await Task.WhenAll(tasks);
            var cacheHits = results.Count(result => result.CacheHit);
            var loaded = results.Count(result => result.Loaded);
            var canceled = results.Count(result => result.Canceled);
            var failures = results.Length - cacheHits - loaded - canceled;
            var elapsed = (long)((System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
            var stats = new LoadStats(elapsed, results.Length, GC.GetTotalMemory(false) - memoryBefore, cacheHits + loaded, failures, cacheHits, loaded, canceled, 0);
            m_ProfilingHook?.Invoke(stats);
            return stats;
        }

        public ValueTask<LoadStats> PreloadAllAsync(CancellationToken cancellationToken = default) => PreheatAsync(Priority.All, cancellationToken);

        public ValueTask<T?> LoadAsync<T>(CancellationToken cancellationToken = default) where T : DataTableBase
            => GetOrCreateDataTableAsync<T>(string.Empty, cancellationToken);

        public ValueTask<T?> LoadAsync<T>(string name, CancellationToken cancellationToken = default) where T : DataTableBase
            => GetOrCreateDataTableAsync<T>(name, cancellationToken);

        public async ValueTask<T?> GetOrCreateDataTableAsync<T>(string name = "", CancellationToken cancellationToken = default) where T : DataTableBase
        {
            ThrowIfDisposed();
            var result = await GetOrCreateDataTableAsync(new TypeNamePair(typeof(T), name ?? string.Empty), cancellationToken);
            return result as T;
        }

        public ValueTask<T?> CreateDataTableAsync<T>(string name = "", CancellationToken cancellationToken = default) where T : DataTableBase
            => GetOrCreateDataTableAsync<T>(name, cancellationToken);

        public T? GetCached<T>(string name = "") where T : DataTableBase
        {
            ThrowIfDisposed();
            lock (m_Gate)
            {
                return TryGetDataTableUnsafe(new TypeNamePair(typeof(T), name ?? string.Empty), out var table) ? table as T : null;
            }
        }

        public bool IsLoaded<T>(string name = "") where T : DataTableBase => GetCached<T>(name) != null;

        public bool HasDataTable(Type dataTableType, string name = "")
        {
            if (dataTableType == null) throw new ArgumentNullException(nameof(dataTableType));
            ThrowIfDisposed();
            lock (m_Gate)
            {
                return TryGetDataTableUnsafe(new TypeNamePair(dataTableType, name ?? string.Empty), out _);
            }
        }

        public bool HasDataTable<T>(string name = "") where T : DataTableBase => IsLoaded<T>(name);

        public T? GetDataTable<T>(string name = "") where T : DataTableBase => GetCached<T>(name);

        public DataTableBase? GetDataTable(Type dataTableType, string name = "")
        {
            if (dataTableType == null) throw new ArgumentNullException(nameof(dataTableType));
            ThrowIfDisposed();
            lock (m_Gate)
            {
                return TryGetDataTableUnsafe(new TypeNamePair(dataTableType, name ?? string.Empty), out var table) ? table : null;
            }
        }

        public DataTableBase[] GetAllDataTables()
        {
            ThrowIfDisposed();
            lock (m_Gate)
            {
                return m_Cache?.Snapshot() ?? m_DataTables.Values.ToArray();
            }
        }

        public void GetAllDataTables(List<DataTableBase> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            results.Clear();
            results.AddRange(GetAllDataTables());
        }

        public void CreateDataTable<T>(Action onCompleted) where T : DataTableBase => CreateDataTable<T>(string.Empty, onCompleted);

        public void CreateDataTable(Type dataTableType, Action onCompleted) => CreateDataTable(dataTableType, string.Empty, onCompleted);

        public void CreateDataTable<T>(string name, Action onCompleted) where T : DataTableBase
        {
            _ = CompleteLegacyCreateAsync(new TypeNamePair(typeof(T), name ?? string.Empty), onCompleted);
        }

        public void CreateDataTable(Type dataTableType, string name, Action onCompleted)
        {
            if (dataTableType == null) throw new ArgumentNullException(nameof(dataTableType));
            _ = CompleteLegacyCreateAsync(new TypeNamePair(dataTableType, name ?? string.Empty), onCompleted);
        }

        public bool DestroyDataTable(DataTableBase dataTable)
        {
            if (dataTable == null) throw new ArgumentNullException(nameof(dataTable));
            return DestroyDataTable(dataTable.GetType(), dataTable.Name);
        }

        public bool DestroyDataTable<T>(string name = "") where T : DataTableBase
        {
            ThrowIfDisposed();
            var pair = new TypeNamePair(typeof(T), name ?? string.Empty);
            DataTableBase? table = null;
            bool removed;
            lock (m_Gate)
            {
                m_TableGenerations.AddOrUpdate(pair, 1, static (_, generation) => generation + 1);
                if (m_Cache != null)
                {
                    removed = m_Cache.Remove(pair);
                }
                else
                {
                    removed = m_DataTables.TryRemove(pair, out table);
                }
                m_LoadingTables.TryRemove(pair, out _);
            }

            ShutdownTable(table);
            return removed;
        }

        public bool DestroyDataTable(Type dataTableType, string name = "")
        {
            if (dataTableType == null) throw new ArgumentNullException(nameof(dataTableType));
            ThrowIfDisposed();
            return DestroyDataTable(new TypeNamePair(dataTableType, name ?? string.Empty));
        }

        public void ClearCache()
        {
            ThrowIfDisposed();
            ShutdownTables(ResetState(dataSource: null, replaceDataSource: false));
        }

        public void OnLoaded<T>(Action<T> onLoaded) where T : DataTableBase
        {
            if (onLoaded == null) throw new ArgumentNullException(nameof(onLoaded));
            ThrowIfDisposed();
            lock (m_Gate)
            {
                if (!m_TypedHooks.TryGetValue(typeof(T), out var hooks))
                {
                    hooks = new List<DataTableLoadedHook>();
                    m_TypedHooks[typeof(T)] = hooks;
                }
                hooks.Add(table => onLoaded((T)table));
            }
        }

        public void OnAnyLoaded(Action<DataTableBase> onLoaded)
        {
            if (onLoaded == null) throw new ArgumentNullException(nameof(onLoaded));
            ThrowIfDisposed();
            lock (m_Gate)
            {
                m_GlobalHooks.Add(new DataTableLoadedHook(onLoaded));
            }
        }

        public void ClearHooks()
        {
            ThrowIfDisposed();
            lock (m_Gate)
            {
                m_TypedHooks.Clear();
                m_GlobalHooks.Clear();
            }
        }

        public LoadStats GetStats()
        {
            ThrowIfDisposed();
            return new LoadStats(
                Interlocked.Read(ref m_TotalLoadTimeMs),
                Count,
                Interlocked.Read(ref m_TotalMemoryDeltaBytes),
                Volatile.Read(ref m_TotalLoadSuccessCount),
                Volatile.Read(ref m_TotalLoadFailureCount));
        }

        public CacheStats? GetCacheStats()
        {
            ThrowIfDisposed();
            lock (m_Gate)
            {
                return m_Cache?.GetStats();
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref m_Disposed, 1) != 0) return;
            ShutdownTables(ResetState(dataSource: null, replaceDataSource: false));
            lock (m_Gate)
            {
                m_DataSource = null;
                m_TypedHooks.Clear();
                m_GlobalHooks.Clear();
                m_TableRegistrations.Clear();
                m_ProfilingHook = null;
            }
        }

        private async ValueTask<DataTableBase?> GetOrCreateDataTableAsync(TypeNamePair pair, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (m_Gate)
            {
                if (TryGetDataTableUnsafe(pair, out var existing))
                {
                    return existing;
                }
                if (m_DataSource == null)
                {
                    throw new InvalidOperationException("DataTableContext requires an IDataSource before loading tables.");
                }
            }

            var lifecycleGeneration = Volatile.Read(ref m_LifecycleGeneration);
            var tableGeneration = m_TableGenerations.GetOrAdd(pair, 0);
            var completion = new TaskCompletionSource<DataTableBase?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var sharedCompletion = m_LoadingTables.GetOrAdd(pair, completion);
            if (ReferenceEquals(sharedCompletion, completion))
            {
                _ = CompleteLoadAsync(pair, completion, lifecycleGeneration, tableGeneration);
            }

            return await AwaitSharedLoadAsync(sharedCompletion.Task, cancellationToken);
        }

        private async Task CompleteLoadAsync(TypeNamePair pair, TaskCompletionSource<DataTableBase?> completion, long lifecycleGeneration, long tableGeneration)
        {
            try
            {
                completion.TrySetResult(await LoadDataTableInternalAsync(pair, lifecycleGeneration, tableGeneration));
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
            finally
            {
                ((ICollection<KeyValuePair<TypeNamePair, TaskCompletionSource<DataTableBase?>>>)m_LoadingTables)
                    .Remove(new KeyValuePair<TypeNamePair, TaskCompletionSource<DataTableBase?>>(pair, completion));
            }
        }

        private async Task CompleteLegacyCreateAsync(TypeNamePair pair, Action? onCompleted)
        {
            try
            {
                await GetOrCreateDataTableAsync(pair, CancellationToken.None);
            }
            catch (Exception exception)
            {
                Log.Error($"Failed to load table {pair}: {exception.Message}", exception);
            }
            finally
            {
                try { onCompleted?.Invoke(); }
                catch (Exception exception) { Log.Error($"Data table completion callback failed: {exception.Message}", exception); }
            }
        }

        private async Task<DataTableBase?> LoadDataTableInternalAsync(TypeNamePair pair, long lifecycleGeneration, long tableGeneration)
        {
            DataTableBase? loadedTable = null;
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            var memoryBefore = GC.GetTotalMemory(false);
            try
            {
                IDataSource source;
                lock (m_Gate)
                {
                    if (!IsLoadCurrent(pair, lifecycleGeneration, tableGeneration)) return null;
                    if (TryGetDataTableUnsafe(pair, out var existing)) return existing;
                    source = m_DataSource ?? throw new InvalidOperationException("DataTableContext requires an IDataSource before loading tables.");
                }

                await using var stream = await source.OpenReadAsync(pair.ToString(), CancellationToken.None);
                if (!IsLoadCurrent(pair, lifecycleGeneration, tableGeneration)) return null;
                loadedTable = DataTableBinaryLoader.Load(pair, stream);
                loadedTable.OnLoadCompleted();

                DataTableBase? published;
                var added = false;
                lock (m_Gate)
                {
                    if (!IsLoadCurrent(pair, lifecycleGeneration, tableGeneration))
                    {
                        published = null;
                    }
                    else if (TryGetDataTableUnsafe(pair, out var existing))
                    {
                        published = existing;
                    }
                    else
                    {
                        StoreDataTableUnsafe(pair, loadedTable);
                        published = loadedTable;
                        added = true;
                    }
                }

                if (published == null)
                {
                    ShutdownTable(loadedTable);
                    return null;
                }

                RecordLoadResult(started, memoryBefore, succeeded: true);
                if (added) TriggerHooks(loadedTable);
                return published;
            }
            catch
            {
                ShutdownTable(loadedTable);
                RecordLoadResult(started, memoryBefore, succeeded: false);
                throw;
            }
        }

        private static async Task<DataTableBase?> AwaitSharedLoadAsync(Task<DataTableBase?> task, CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled) return await task;
            var cancellation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = cancellationToken.Register(static state => ((TaskCompletionSource<bool>)state!).TrySetResult(true), cancellation);
            if (await Task.WhenAny(task, cancellation.Task) != task)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
            return await task;
        }

        private DataTableBase[] ResetState(IDataSource? dataSource, bool replaceDataSource = true)
        {
            lock (m_Gate)
            {
                Interlocked.Increment(ref m_LifecycleGeneration);
                m_TableGenerations.Clear();
                m_Cache?.Clear();
                var tables = m_DataTables.Values.ToArray();
                m_DataTables.Clear();
                m_LoadingTables.Clear();
                if (replaceDataSource) m_DataSource = dataSource;
                return tables;
            }
        }

        private bool TryGetDataTableUnsafe(TypeNamePair pair, out DataTableBase? table)
        {
            return m_Cache != null ? m_Cache.TryGet<DataTableBase>(pair, out table) : m_DataTables.TryGetValue(pair, out table);
        }

        private bool DestroyDataTable(TypeNamePair pair)
        {
            DataTableBase? table = null;
            bool removed;
            lock (m_Gate)
            {
                m_TableGenerations.AddOrUpdate(pair, 1, static (_, generation) => generation + 1);
                if (m_Cache != null) removed = m_Cache.Remove(pair);
                else removed = m_DataTables.TryRemove(pair, out table);
                m_LoadingTables.TryRemove(pair, out _);
            }
            ShutdownTable(table);
            return removed;
        }

        private void StoreDataTableUnsafe(TypeNamePair pair, DataTableBase table)
        {
            if (m_Cache != null) m_Cache.Set(pair, table);
            else m_DataTables[pair] = table;
        }

        private bool IsLoadCurrent(TypeNamePair pair, long lifecycleGeneration, long tableGeneration)
        {
            if (lifecycleGeneration != Volatile.Read(ref m_LifecycleGeneration)) return false;
            return !m_TableGenerations.TryGetValue(pair, out var current) ? tableGeneration == 0 : current == tableGeneration;
        }

        private void TriggerHooks(DataTableBase table)
        {
            DataTableLoadedHook[] typed;
            DataTableLoadedHook[] global;
            lock (m_Gate)
            {
                typed = m_TypedHooks.TryGetValue(table.GetType(), out var hooks) ? hooks.ToArray() : Array.Empty<DataTableLoadedHook>();
                global = m_GlobalHooks.ToArray();
            }
            foreach (var hook in typed)
            {
                try { hook(table); } catch { }
            }
            foreach (var hook in global)
            {
                try { hook(table); } catch { }
            }
        }

        private void RecordLoadResult(long startTimestamp, long memoryBefore, bool succeeded)
        {
            if (succeeded)
            {
                var elapsed = (long)((System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
                Interlocked.Add(ref m_TotalLoadTimeMs, elapsed);
                Interlocked.Add(ref m_TotalMemoryDeltaBytes, GC.GetTotalMemory(false) - memoryBefore);
                Interlocked.Increment(ref m_TotalLoadSuccessCount);
            }
            else
            {
                Interlocked.Increment(ref m_TotalLoadFailureCount);
            }
        }

        private static void ShutdownTables(IEnumerable<DataTableBase> tables)
        {
            foreach (var table in tables) ShutdownTable(table);
        }

        private static void ShutdownTable(DataTableBase? table)
        {
            if (table == null) return;
            try { table.Shutdown(); } catch { }
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref m_Disposed) != 0)
            {
                throw new ObjectDisposedException(nameof(DataTableContext));
            }
        }

        private static string GetRegistrationKey(TableRegistration registration)
        {
            return (registration.TableType.AssemblyQualifiedName ?? registration.TableType.FullName ?? registration.TableType.Name) + "|" + registration.Name;
        }
    }
}
