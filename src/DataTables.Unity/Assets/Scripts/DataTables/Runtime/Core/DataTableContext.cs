using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataTables
{
    /// <summary>
    /// Controls where synchronous binary deserialization runs after the payload has been read asynchronously.
    /// </summary>
    public enum DataTableParseExecution
    {
        /// <summary>
        /// Parse on a thread-pool worker after asynchronous payload I/O. Unity WebGL Player falls back to
        /// <see cref="CallingContext"/> because background threads are unavailable.
        /// </summary>
        BackgroundThread,

        /// <summary>
        /// Parse on the async continuation after payload I/O. A captured Unity synchronization context keeps
        /// this on the main thread; without a synchronization context, no particular thread is guaranteed.
        /// </summary>
        CallingContext
    }

    /// <summary>
    /// 独立的数据表运行时上下文。每个实例拥有自己的数据源、加载任务、缓存、生命周期和 Hook。
    /// </summary>
    public sealed class DataTableContext : IDataTableContext, IDisposable
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
        private CancellationTokenSource? m_LifecycleCancellation = new();
        private DataTableParseExecution m_ParseExecution = DataTableParseExecution.BackgroundThread;
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

        public bool IsEstimatedMemoryBudgetEnabled
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

        /// <summary>
        /// Gets or sets where synchronous payload deserialization executes. Payload I/O is always asynchronous.
        /// </summary>
        public DataTableParseExecution ParseExecution
        {
            get
            {
                ThrowIfDisposed();
                lock (m_Gate)
                {
                    return m_ParseExecution;
                }
            }
            set
            {
                if (!Enum.IsDefined(typeof(DataTableParseExecution), value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                ThrowIfDisposed();
                lock (m_Gate)
                {
                    m_ParseExecution = value;
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

        public void EnableEstimatedMemoryBudget(int maxEstimatedMemoryMB, Func<DataTableBase, long>? estimatedSizeProvider = null)
        {
            if (maxEstimatedMemoryMB <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxEstimatedMemoryMB), "Estimated memory budget must be greater than zero.");
            }

            ThrowIfDisposed();
            var estimatedBudgetBytes = (long)maxEstimatedMemoryMB * 1024 * 1024;
            var cache = estimatedSizeProvider == null
                ? new LRUDataTableCache(estimatedBudgetBytes)
                : new LRUDataTableCache(estimatedBudgetBytes, estimatedSizeProvider);
            LRUDataTableCache? previousCache;
            lock (m_Gate)
            {
                previousCache = m_Cache;
                var tables = previousCache != null
                    ? previousCache.Drain()
                    : m_DataTables.Select(pair => new KeyValuePair<TypeNamePair, DataTableBase>(pair.Key, pair.Value)).ToArray();
                m_DataTables.Clear();
                m_Cache = cache;
                foreach (var table in tables)
                {
                    if (!cache.TrySet(table.Key, table.Value)) ShutdownTable(table.Value);
                }
            }
            previousCache?.Dispose();
        }

        public void DisableEstimatedMemoryBudget()
        {
            ThrowIfDisposed();
            LRUDataTableCache? cache;
            lock (m_Gate)
            {
                if (m_Cache == null) return;
                cache = m_Cache;
                var tables = cache.Drain();
                m_Cache = null;
                foreach (var table in tables)
                {
                    m_DataTables[table.Key] = table.Value;
                }
            }
            cache.Dispose();
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

        public ValueTask<PreheatResult> PreheatAsync(Priority priorities = Priority.Critical | Priority.Normal, CancellationToken cancellationToken = default)
            => PreheatAsync(priorities, PreheatOptions.Default, cancellationToken);

        public async ValueTask<PreheatResult> PreheatAsync(Priority priorities, PreheatOptions options, CancellationToken cancellationToken = default)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            var memoryBefore = GC.GetTotalMemory(false);
            var registrations = m_TableRegistrations.Values
                .Where(registration => (priorities & registration.Priority) != 0)
                .OrderBy(registration => registration.TableType.FullName ?? registration.TableType.Name, StringComparer.Ordinal)
                .ThenBy(registration => registration.Name, StringComparer.Ordinal)
                .ToArray();

            if (registrations.Length == 0)
            {
                return new PreheatResult(new LoadStats(0, 0, 0, 0, 0, 0, 0, 0, 0, 1), Array.Empty<PreheatTableResult>(), PreheatStopReason.None);
            }

            var results = new PreheatTableResult?[registrations.Length];
            var schedulerGate = new object();
            var nextIndex = 0;
            var stopReason = PreheatStopReason.None;

            async Task RunWorkerAsync()
            {
                while (true)
                {
                    int index;
                    lock (schedulerGate)
                    {
                        if (stopReason != PreheatStopReason.None || nextIndex >= registrations.Length)
                        {
                            return;
                        }

                        if (cancellationToken.IsCancellationRequested)
                        {
                            stopReason = PreheatStopReason.Canceled;
                            return;
                        }

                        index = nextIndex++;
                    }

                    var result = await PreheatTableAsync(registrations[index], cancellationToken);
                    results[index] = result;
                    lock (schedulerGate)
                    {
                        if (result.Status == PreheatTableStatus.Canceled)
                        {
                            stopReason = PreheatStopReason.Canceled;
                        }
                        else if (result.Status == PreheatTableStatus.Failed && options.FailFast && stopReason == PreheatStopReason.None)
                        {
                            stopReason = PreheatStopReason.FailFast;
                        }
                    }
                }
            }

            var workerCount = Math.Min(options.MaxConcurrency, registrations.Length);
            var workers = new Task[workerCount];
            for (var i = 0; i < workers.Length; i++) workers[i] = RunWorkerAsync();
            await Task.WhenAll(workers);

            if (cancellationToken.IsCancellationRequested
                && stopReason == PreheatStopReason.None
                && nextIndex < registrations.Length)
            {
                stopReason = PreheatStopReason.Canceled;
            }

            for (var i = 0; i < results.Length; i++)
            {
                results[i] ??= new PreheatTableResult(
                    registrations[i].TableType,
                    registrations[i].Name,
                    registrations[i].Priority,
                    PreheatTableStatus.NotStarted,
                    0);
            }

            var tableResults = results.Select(result => result!).ToArray();
            var cacheHits = tableResults.Count(result => result.Status == PreheatTableStatus.CacheHit);
            var loaded = tableResults.Count(result => result.Status == PreheatTableStatus.Loaded);
            var canceled = tableResults.Count(result => result.Status == PreheatTableStatus.Canceled);
            var failures = tableResults.Count(result => result.Status == PreheatTableStatus.Failed);
            var notStarted = tableResults.Count(result => result.Status == PreheatTableStatus.NotStarted);
            var elapsed = (long)((System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
            var stats = new LoadStats(elapsed, tableResults.Length, GC.GetTotalMemory(false) - memoryBefore, cacheHits + loaded, failures, cacheHits, loaded, canceled, notStarted, 0);
            m_ProfilingHook?.Invoke(stats);
            return new PreheatResult(stats, tableResults, stopReason);
        }

        public ValueTask<PreheatResult> PreloadAllAsync(CancellationToken cancellationToken = default) => PreheatAsync(Priority.All, cancellationToken);

        public ValueTask<PreheatResult> PreloadAllAsync(PreheatOptions options, CancellationToken cancellationToken = default) => PreheatAsync(Priority.All, options, cancellationToken);

        private async ValueTask<PreheatTableResult> PreheatTableAsync(TableRegistration registration, CancellationToken cancellationToken)
        {
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            PreheatTableResult CreateResult(PreheatTableStatus status, Exception? exception = null)
            {
                var elapsed = (long)((System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
                return new PreheatTableResult(registration.TableType, registration.Name, registration.Priority, status, elapsed, exception);
            }

            if (IsLoaded(new TypeNamePair(registration.TableType, registration.Name)))
            {
                return CreateResult(PreheatTableStatus.CacheHit);
            }

            try
            {
                var table = await registration.LoadAsync(this, cancellationToken);
                return table != null
                    ? CreateResult(PreheatTableStatus.Loaded)
                    : CreateResult(PreheatTableStatus.Failed, new InvalidOperationException($"Preheat returned null for '{registration.TableType.FullName}' ({registration.Name})."));
            }
            catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
            {
                return CreateResult(PreheatTableStatus.Canceled, exception);
            }
            catch (Exception exception)
            {
                return CreateResult(PreheatTableStatus.Failed, exception);
            }
        }

        public async ValueTask<T?> LoadAsync<T>(string name = "", CancellationToken cancellationToken = default) where T : DataTableBase
        {
            ThrowIfDisposed();
            var result = await GetOrCreateDataTableAsync(new TypeNamePair(typeof(T), name ?? string.Empty), cancellationToken);
            return result as T;
        }

        public T? GetCached<T>(string name = "") where T : DataTableBase
        {
            ThrowIfDisposed();
            lock (m_Gate)
            {
                return TryGetDataTableUnsafe(new TypeNamePair(typeof(T), name ?? string.Empty), out var table) ? table as T : null;
            }
        }

        public bool IsLoaded<T>(string name = "") where T : DataTableBase => GetCached<T>(name) != null;

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
            LRUDataTableCache? cache;
            lock (m_Gate)
            {
                cache = m_Cache;
                m_Cache = null;
                m_DataSource = null;
                m_TypedHooks.Clear();
                m_GlobalHooks.Clear();
                m_TableRegistrations.Clear();
                m_ProfilingHook = null;
            }
            cache?.Dispose();
        }

        private async ValueTask<DataTableBase?> GetOrCreateDataTableAsync(TypeNamePair pair, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            long lifecycleGeneration;
            long tableGeneration;
            CancellationToken lifecycleToken;
            DataTableParseExecution parseExecution;
            TaskCompletionSource<DataTableBase?> completion;
            TaskCompletionSource<DataTableBase?> sharedCompletion;
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

                var lifecycleCancellation = m_LifecycleCancellation ?? throw new ObjectDisposedException(nameof(DataTableContext));
                lifecycleGeneration = m_LifecycleGeneration;
                tableGeneration = m_TableGenerations.GetOrAdd(pair, 0);
                lifecycleToken = lifecycleCancellation.Token;
                parseExecution = m_ParseExecution;
                completion = new TaskCompletionSource<DataTableBase?>(TaskCreationOptions.RunContinuationsAsynchronously);
                sharedCompletion = m_LoadingTables.GetOrAdd(pair, completion);
            }

            if (ReferenceEquals(sharedCompletion, completion))
            {
                _ = CompleteLoadAsync(pair, completion, lifecycleGeneration, tableGeneration, lifecycleToken, parseExecution);
            }

            return await AwaitSharedLoadAsync(sharedCompletion.Task, cancellationToken);
        }

        private async Task CompleteLoadAsync(TypeNamePair pair, TaskCompletionSource<DataTableBase?> completion, long lifecycleGeneration, long tableGeneration, CancellationToken lifecycleToken, DataTableParseExecution parseExecution)
        {
            try
            {
                completion.TrySetResult(await LoadDataTableInternalAsync(pair, lifecycleGeneration, tableGeneration, lifecycleToken, parseExecution));
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

        private async Task<DataTableBase?> LoadDataTableInternalAsync(TypeNamePair pair, long lifecycleGeneration, long tableGeneration, CancellationToken lifecycleToken, DataTableParseExecution parseExecution)
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

                await using var stream = await source.OpenReadAsync(pair.ToString(), lifecycleToken);
                await using var payload = new MemoryStream();
                await stream.CopyToAsync(payload, 81920, lifecycleToken);
                lifecycleToken.ThrowIfCancellationRequested();
                if (!IsLoadCurrent(pair, lifecycleGeneration, tableGeneration)) return null;
                payload.Position = 0;
                loadedTable = parseExecution == DataTableParseExecution.BackgroundThread && SupportsBackgroundParsing
                    ? await Task.Run(() => DataTableBinaryLoader.Load(pair, payload), lifecycleToken)
                    : DataTableBinaryLoader.Load(pair, payload);
                lifecycleToken.ThrowIfCancellationRequested();
                if (!IsLoadCurrent(pair, lifecycleGeneration, tableGeneration))
                {
                    ShutdownTable(loadedTable);
                    loadedTable = null;
                    return null;
                }
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
            catch (OperationCanceledException) when (lifecycleToken.IsCancellationRequested && !IsLoadCurrent(pair, lifecycleGeneration, tableGeneration))
            {
                ShutdownTable(loadedTable);
                return null;
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
            DataTableBase[] tables;
            CancellationTokenSource? lifecycleCancellation;
            lock (m_Gate)
            {
                Interlocked.Increment(ref m_LifecycleGeneration);
                lifecycleCancellation = m_LifecycleCancellation;
                m_LifecycleCancellation = Volatile.Read(ref m_Disposed) == 0 ? new CancellationTokenSource() : null;
                m_TableGenerations.Clear();
                m_Cache?.Clear();
                tables = m_DataTables.Values.ToArray();
                m_DataTables.Clear();
                m_LoadingTables.Clear();
                if (replaceDataSource) m_DataSource = dataSource;
            }

            CancelAndDispose(lifecycleCancellation);
            return tables;
        }

        private bool TryGetDataTableUnsafe(TypeNamePair pair, out DataTableBase? table)
        {
            return m_Cache != null ? m_Cache.TryGet<DataTableBase>(pair, out table) : m_DataTables.TryGetValue(pair, out table);
        }

        private bool IsLoaded(TypeNamePair pair)
        {
            lock (m_Gate)
            {
                return TryGetDataTableUnsafe(pair, out _);
            }
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
            if (m_Cache != null) m_Cache.TrySet(pair, table);
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

        private static void CancelAndDispose(CancellationTokenSource? cancellation)
        {
            if (cancellation == null) return;
            try { cancellation.Cancel(); }
            catch (Exception exception) { Log.Error($"Data table lifecycle cancellation callback failed: {exception.Message}", exception); }
            finally { cancellation.Dispose(); }
        }

        private static bool SupportsBackgroundParsing
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return false;
#else
                return true;
#endif
            }
        }

        private static string GetRegistrationKey(TableRegistration registration)
        {
            return (registration.TableType.AssemblyQualifiedName ?? registration.TableType.FullName ?? registration.TableType.Name) + "|" + registration.Name;
        }
    }
}
