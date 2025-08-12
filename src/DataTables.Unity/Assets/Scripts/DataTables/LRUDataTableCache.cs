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

            if (_cache.TryGetValue(key, out var node))
            {
                _lock.EnterWriteLock();
                try
                {
                    // 移动到头部 (最近使用)
                    _lruList.Remove(node);
                    _lruList.AddFirst(node);
                    node.Value.LastAccessed = DateTime.UtcNow;
                }
                finally
                {
                    _lock.ExitWriteLock();
                }

                Interlocked.Increment(ref _hitCount);
                table = (T)node.Value.Table;
                return true;
            }

            table = null;
            return false;
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

            _lock.EnterWriteLock();
            try
            {
                // 如果已存在，先移除
                if (_cache.TryGetValue(key, out var existingNode))
                {
                    _lruList.Remove(existingNode);
                    _cache.TryRemove(key, out _);
                    Interlocked.Add(ref _currentMemoryUsage, -existingNode.Value.MemorySize);
                }

                // 淘汰旧项目直到有足够空间
                while (_currentMemoryUsage + memorySize > _maxMemoryBytes && _lruList.Count > 0)
                {
                    EvictLeastRecentlyUsed();
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
        }

        /// <summary>
        /// 清除所有缓存项
        /// </summary>
        public void Clear()
        {
            _lock.EnterWriteLock();
            try
            {
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
        private void EvictLeastRecentlyUsed()
        {
            if (_lruList.Last != null)
            {
                var lastNode = _lruList.Last;
                var item = lastNode.Value;

                _lruList.RemoveLast();
                _cache.TryRemove(item.Key, out _);
                Interlocked.Add(ref _currentMemoryUsage, -item.MemorySize);

                // 清理被淘汰的表
                try
                {
                    item.Table.Shutdown();
                }
                catch
                {
                    // 静默失败
                }
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


    /// <summary>
    /// 内存管理器 - 集成LRU缓存到DataTableManager
    /// </summary>
    public static class DataTableMemoryManager
    {
        private static LRUDataTableCache? s_Cache;
        private static volatile bool s_Enabled = false;

        /// <summary>
        /// 启用内存管理
        /// </summary>
        /// <param name="maxMemoryMB">最大内存使用量(MB)</param>
        public static void Enable(int maxMemoryMB)
        {
            var maxBytes = (long)maxMemoryMB * 1024 * 1024;
            s_Cache = new LRUDataTableCache(maxBytes);
            s_Enabled = true;
        }

        /// <summary>
        /// 禁用内存管理
        /// </summary>
        public static void Disable()
        {
            s_Enabled = false;
            s_Cache?.Dispose();
            s_Cache = null;
        }

        /// <summary>
        /// 尝试从缓存获取
        /// </summary>
        internal static bool TryGetFromCache<T>(TypeNamePair key, out T? table) where T : DataTableBase
        {
            table = null;
            return s_Enabled && s_Cache != null && s_Cache.TryGet(key, out table);
        }

        /// <summary>
        /// 添加到缓存
        /// </summary>
        internal static void AddToCache<T>(TypeNamePair key, T table) where T : DataTableBase
        {
            if (s_Enabled && s_Cache != null)
            {
                s_Cache.Set(key, table);
            }
        }

        /// <summary>
        /// 获取缓存统计
        /// </summary>
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
        }
    }
}
