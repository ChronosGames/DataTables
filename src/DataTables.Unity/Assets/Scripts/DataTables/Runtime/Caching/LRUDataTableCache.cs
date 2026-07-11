using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace DataTables
{
    /// <summary>
    /// LRU数据表缓存实现 - 智能内存管理
    /// </summary>
    public class LRUDataTableCache
    {
        private readonly ConcurrentDictionary<TypeNamePair, LinkedListNode<CacheItem>> _cache = new();
        private readonly LinkedList<CacheItem> _lruList = new();
        private readonly ReaderWriterLockSlim _lock = new();
        private readonly long _maxMemoryBytes;
        private long _currentMemoryUsage;
        private int _accessCount;
        private int _hitCount;

        /// <summary>
        /// 缓存项
        /// </summary>
        private sealed class CacheItem
        {
            public TypeNamePair Key { get; }
            public DataTableBase Table { get; }
            public long MemorySize { get; }
            public DateTime LastAccessed { get; set; }

            public CacheItem(TypeNamePair key, DataTableBase table, long memorySize)
            {
                Key = key;
                Table = table;
                MemorySize = memorySize;
                LastAccessed = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// 创建LRU缓存
        /// </summary>
        /// <param name="maxMemoryBytes">最大内存使用量(字节)</param>
        public LRUDataTableCache(long maxMemoryBytes)
        {
            if (maxMemoryBytes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxMemoryBytes), "Cache memory limit must be greater than zero.");
            }

            _maxMemoryBytes = maxMemoryBytes;
        }

        /// <summary>
        /// 获取缓存中的数据表
        /// </summary>
        /// <typeparam name="T">数据表类型</typeparam>
        /// <param name="key">表键</param>
        /// <param name="table">输出的表实例</param>
        /// <returns>是否找到</returns>
        public bool TryGet<T>(TypeNamePair key, out T? table) where T : DataTableBase
        {
            Interlocked.Increment(ref _accessCount);

            _lock.EnterWriteLock();
            try
            {
                if (_cache.TryGetValue(key, out var node))
                {
                    // 移动到头部 (最近使用)
                    _lruList.Remove(node);
                    _lruList.AddFirst(node);
                    node.Value.LastAccessed = DateTime.UtcNow;

                    Interlocked.Increment(ref _hitCount);
                    table = (T)node.Value.Table;
                    return true;
                }

                table = null;
                return false;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Checks whether a table exists in the cache without updating LRU hit statistics.
        /// </summary>
        public bool Contains(TypeNamePair key)
        {
            _lock.EnterReadLock();
            try
            {
                return _cache.ContainsKey(key);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// 添加数据表到缓存
        /// </summary>
        /// <typeparam name="T">数据表类型</typeparam>
        /// <param name="key">表键</param>
        /// <param name="table">表实例</param>
        public void Set<T>(TypeNamePair key, T table) where T : DataTableBase
        {
            var memorySize = EstimateMemoryUsage(table);
            List<DataTableBase>? removedTables = null;

            _lock.EnterWriteLock();
            try
            {
                // 如果已存在，先移除
                if (_cache.TryGetValue(key, out var existingNode))
                {
                    _lruList.Remove(existingNode);
                    _cache.TryRemove(key, out _);
                    Interlocked.Add(ref _currentMemoryUsage, -existingNode.Value.MemorySize);
                    if (!ReferenceEquals(existingNode.Value.Table, table))
                    {
                        removedTables = new List<DataTableBase> { existingNode.Value.Table };
                    }
                }

                // 淘汰旧项目直到有足够空间
                while (_currentMemoryUsage + memorySize > _maxMemoryBytes && _lruList.Count > 0)
                {
                    removedTables ??= new List<DataTableBase>();
                    EvictLeastRecentlyUsed(removedTables);
                }

                // 添加新项目
                var item = new CacheItem(key, table, memorySize);
                var node = new LinkedListNode<CacheItem>(item);
                _lruList.AddFirst(node);
                _cache[key] = node;
                Interlocked.Add(ref _currentMemoryUsage, memorySize);
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            ShutdownTables(removedTables);
        }

        /// <summary>
        /// 清除所有缓存项
        /// </summary>
        public void Clear()
        {
            List<DataTableBase>? removedTables = null;
            _lock.EnterWriteLock();
            try
            {
                if (_lruList.Count > 0)
                {
                    removedTables = new List<DataTableBase>(_lruList.Count);
                    foreach (var item in _lruList)
                    {
                        removedTables.Add(item.Table);
                    }
                }

                _cache.Clear();
                _lruList.Clear();
                Interlocked.Exchange(ref _currentMemoryUsage, 0);
                Interlocked.Exchange(ref _accessCount, 0);
                Interlocked.Exchange(ref _hitCount, 0);
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            ShutdownTables(removedTables);
        }

        /// <summary>
        /// 移除并关闭指定缓存项。
        /// </summary>
        public bool Remove(TypeNamePair key)
        {
            DataTableBase? removedTable = null;

            _lock.EnterWriteLock();
            try
            {
                if (!_cache.TryRemove(key, out var node))
                {
                    return false;
                }

                _lruList.Remove(node);
                Interlocked.Add(ref _currentMemoryUsage, -node.Value.MemorySize);
                removedTable = node.Value.Table;
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            Shutdown(removedTable);
            return true;
        }

        /// <summary>
        /// 移交所有缓存项的所有权而不关闭它们。
        /// </summary>
        public KeyValuePair<TypeNamePair, DataTableBase>[] Drain()
        {
            KeyValuePair<TypeNamePair, DataTableBase>[] tables;

            _lock.EnterWriteLock();
            try
            {
                tables = new KeyValuePair<TypeNamePair, DataTableBase>[_lruList.Count];
                var index = 0;
                foreach (var item in _lruList)
                {
                    tables[index++] = new KeyValuePair<TypeNamePair, DataTableBase>(item.Key, item.Table);
                }

                _cache.Clear();
                _lruList.Clear();
                Interlocked.Exchange(ref _currentMemoryUsage, 0);
                Interlocked.Exchange(ref _accessCount, 0);
                Interlocked.Exchange(ref _hitCount, 0);
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            return tables;
        }

        /// <summary>
        /// 获取当前缓存表快照，不改变 LRU 顺序或命中统计。
        /// </summary>
        public DataTableBase[] Snapshot()
        {
            _lock.EnterReadLock();
            try
            {
                var tables = new DataTableBase[_lruList.Count];
                var index = 0;
                foreach (var item in _lruList)
                {
                    tables[index++] = item.Table;
                }
                return tables;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        /// <returns>缓存统计</returns>
        public CacheStats GetStats()
        {
            var accessCount = _accessCount;
            var hitCount = _hitCount;
            var hitRate = accessCount > 0 ? (float)hitCount / accessCount : 0f;

            _lock.EnterReadLock();
            try
            {
                return new CacheStats(
                    _cache.Count,
                    _currentMemoryUsage,
                    accessCount,
                    hitCount,
                    (float)_currentMemoryUsage / _maxMemoryBytes,
                    DateTime.UtcNow
                );
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// 淘汰最少使用的项
        /// </summary>
        private void EvictLeastRecentlyUsed(List<DataTableBase> removedTables)
        {
            if (_lruList.Last != null)
            {
                var lastNode = _lruList.Last;
                var item = lastNode.Value;

                _lruList.RemoveLast();
                _cache.TryRemove(item.Key, out _);
                Interlocked.Add(ref _currentMemoryUsage, -item.MemorySize);
                removedTables.Add(item.Table);
            }
        }

        private static void ShutdownTables(List<DataTableBase>? tables)
        {
            if (tables == null) return;

            foreach (var table in tables)
            {
                Shutdown(table);
            }
        }

        private static void Shutdown(DataTableBase? table)
        {
            if (table == null) return;

            try
            {
                table.Shutdown();
            }
            catch
            {
                // 静默失败
            }
        }

        /// <summary>
        /// 估算数据表内存使用量
        /// </summary>
        /// <param name="table">数据表</param>
        /// <returns>估算内存大小(字节)</returns>
        private static long EstimateMemoryUsage(DataTableBase table)
        {
            // 简单的内存估算：基础开销 + 每行估算开销
            const long baseOverhead = 1024; // 1KB基础开销
            const long averageRowSize = 256; // 256字节平均行大小

            return baseOverhead + (table.Count * averageRowSize);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Clear();
            _lock.Dispose();
        }
    }
}
