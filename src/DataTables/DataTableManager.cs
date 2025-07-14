using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace DataTables
{
    /// <summary>
    /// 数据表加载完成后的Hook委托
    /// </summary>
    /// <typeparam name="T">数据表类型</typeparam>
    /// <param name="dataTable">已加载的数据表</param>
    public delegate void DataTableLoadedHook<T>(T dataTable) where T : DataTableBase;

    /// <summary>
    /// 通用的数据表加载完成Hook委托
    /// </summary>
    /// <param name="dataTable">已加载的数据表</param>
    public delegate void DataTableLoadedHook(DataTableBase dataTable);


    public static class DataTableManager
    {
        private static readonly ConcurrentDictionary<TypeNamePair, DataTableBase> s_DataTables = new();
        private static IDataTableHelper? s_DataTableHelper;
        private static readonly object s_Lock = new object();

        // Hook机制
        private static readonly ConcurrentDictionary<Type, List<DataTableLoadedHook>> s_TypedHooks = new();
        private static readonly List<DataTableLoadedHook> s_GlobalHooks = new();

        /// <summary>
        /// 获取数据表数量。
        /// </summary>
        public static int Count => s_DataTables.Count;

        /// <summary>
        /// 设置数据表辅助器。
        /// </summary>
        /// <param name="dataTableHelper">数据表辅助器。</param>
        public static void SetDataTableHelper(IDataTableHelper dataTableHelper)
        {
            s_DataTableHelper = dataTableHelper ?? throw new Exception("Data table helper is invalid.");
        }

        /// <summary>
        /// 注册特定类型的数据表加载完成Hook
        /// </summary>
        /// <typeparam name="T">数据表类型</typeparam>
        /// <param name="hook">Hook回调</param>
        public static void HookDataTableLoaded<T>(DataTableLoadedHook<T> hook) where T : DataTableBase
        {
            var type = typeof(T);
            var wrappedHook = new DataTableLoadedHook(table => hook((T)table));

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
        /// <param name="hook">Hook回调</param>
        public static void HookGlobalDataTableLoaded(DataTableLoadedHook hook)
        {
            lock (s_Lock)
            {
                s_GlobalHooks.Add(hook);
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

        /// <summary>
        /// 关闭并清理数据表管理器。
        /// </summary>
        public static void Shutdown()
        {
            lock (s_Lock)
            {
                foreach (var dataTable in s_DataTables)
                {
                    dataTable.Value.Shutdown();
                }

                s_DataTables.Clear();
            }
        }

        /// <summary>
        /// 是否存在数据表。
        /// </summary>
        /// <typeparam name="T">数据表行的类型。</typeparam>
        /// <param name="name">数据表名称。</param>
        /// <returns>是否存在数据表。</returns>
        public static bool HasDataTable<T>(string name = "") where T : DataTableBase
        {
            return InternalHasDataTable(new TypeNamePair(typeof(T), name));
        }

        /// <summary>
        /// 是否存在数据表。
        /// </summary>
        /// <param name="dataTableType">数据表的类型。</param>
        /// <param name="name">数据表名称。</param>
        /// <returns>是否存在数据表。</returns>
        public static bool HasDataTable(Type dataTableType, string name = "")
        {
            if (dataTableType == null)
            {
                throw new Exception("Data table type is invalid.");
            }

            if (!dataTableType.IsSubclassOf(typeof(DataTableBase)))
            {
                throw new Exception($"Data row type '{dataTableType.FullName}' is invalid.");
            }

            return InternalHasDataTable(new TypeNamePair(dataTableType, name));
        }

        /// <summary>
        /// 获取数据表。
        /// </summary>
        /// <typeparam name="T">数据表的类型。</typeparam>
        /// <param name="name">数据表名称。</param>
        /// <returns>要获取的数据表。</returns>
        public static T? GetDataTable<T>(string name = "") where T : DataTableBase
        {
            var dataTable = InternalGetDataTable(new TypeNamePair(typeof(T), name));
            return dataTable != null ? (T)dataTable : null;
        }

        /// <summary>
        /// 获取数据表。
        /// </summary>
        /// <param name="dataTableType">数据表的类型。</param>
        /// <param name="name">数据表名称。</param>
        /// <returns>要获取的数据表。</returns>
        public static DataTableBase? GetDataTable(Type dataTableType, string name = "")
        {
            if (dataTableType == null)
            {
                throw new Exception("Data table type is invalid.");
            }

            if (!dataTableType.IsSubclassOf(typeof(DataTableBase)))
            {
                throw new Exception($"Data table type '{dataTableType.FullName}' is invalid.");
            }

            return InternalGetDataTable(new TypeNamePair(dataTableType, name));
        }

        /// <summary>
        /// 获取所有数据表。
        /// </summary>
        /// <returns>所有数据表。</returns>
        public static DataTableBase[] GetAllDataTables()
        {
            lock (s_Lock)
            {
                int index = 0;
                DataTableBase[] results = new DataTableBase[s_DataTables.Count];
                foreach (var dataTable in s_DataTables)
                {
                    results[index++] = dataTable.Value;
                }

                return results;
            }
        }

        /// <summary>
        /// 获取所有数据表。
        /// </summary>
        /// <param name="results">所有数据表。</param>
        public static void GetAllDataTables(List<DataTableBase> results)
        {
            if (results == null)
            {
                throw new Exception("Results is invalid.");
            }

            lock (s_Lock)
            {
                results.Clear();
                results.AddRange(s_DataTables.Select(dataTable => dataTable.Value));
            }
        }

        /// <summary>
        /// 创建数据表。
        /// </summary>
        /// <typeparam name="T">数据表的类型。</typeparam>
        /// <param name="onCompleted">数据表加载完成时回调。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CreateDataTable<T>(Action onCompleted) where T : DataTableBase
        {
            CreateDataTable<T>(string.Empty, onCompleted);
        }

        /// <summary>
        /// 创建数据表。
        /// </summary>
        /// <param name="dataTableType">数据表的类型。</param>
        /// <param name="onCompleted">数据表加载完成时回调。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CreateDataTable(Type dataTableType, Action onCompleted)
        {
            CreateDataTable(dataTableType, string.Empty, onCompleted);
        }

        /// <summary>
        /// 创建数据表。
        /// </summary>
        /// <typeparam name="T">数据表的类型。</typeparam>
        /// <param name="name">数据表名称。</param>
        /// <param name="onCompleted">数据表加载完成时回调。</param>
        public static void CreateDataTable<T>(string name, Action onCompleted) where T : DataTableBase
        {
            if (s_DataTableHelper == null)
            {
                throw new Exception("YOU MUST SetDataTableHelper FIRST.");
            }

            var typeNamePair = new TypeNamePair(typeof(T), name);
            if (InternalHasDataTable(typeNamePair))
            {
                throw new Exception($"Already exist data table '{typeNamePair}'.");
            }

            s_DataTableHelper.Read(typeNamePair.ToString(), (raw) => LoadDataTable(typeNamePair, raw, onCompleted));
        }

        /// <summary>
        /// 创建数据表。
        /// </summary>
        /// <param name="dataTableType">数据表的类型。</param>
        /// <param name="name">数据表名称。</param>
        /// <param name="onCompleted">数据表加载完成时回调。</param>
        public static void CreateDataTable(Type dataTableType, string name, Action onCompleted)
        {
            if (!dataTableType.IsSubclassOf(typeof(DataTableBase)))
            {
                throw new Exception($"Data table type '{dataTableType.FullName}' is invalid.");
            }

            if (s_DataTableHelper == null)
            {
                throw new Exception("MUST SetDataTableHelper FIRST.");
            }

            var typeNamePair = new TypeNamePair(dataTableType, name);
            if (InternalHasDataTable(typeNamePair))
            {
                throw new Exception($"Already exist data table '{typeNamePair}'.");
            }

            s_DataTableHelper.Read(typeNamePair.ToString(), (raw) => LoadDataTable(typeNamePair, raw, onCompleted));
        }

        private static void LoadDataTable(TypeNamePair typeNamePair, byte[] raw, Action? onCompleted)
        {
            DataTableBase dataTable;

            lock (s_Lock)
            {
                if (InternalHasDataTable(typeNamePair))
                {
                    throw new Exception($"Already exist data table '{typeNamePair}'.");
                }

                using (var ms = new MemoryStream(raw, false))
                {
                    using (var reader = new BinaryReader(ms, Encoding.UTF8))
                    {
                        int rowCount = reader.ReadInt32();
                        dataTable = (DataTableBase)Activator.CreateInstance(typeNamePair.Type, typeNamePair.Name, rowCount)!;
                        for (int i = 0; i < rowCount; i++)
                        {
                            if (!dataTable.ParseDataRow(i, reader))
                            {
                                return;
                            }
                        }
                    }
                }

                // 触发加载完成事件
                dataTable.OnLoadCompleted();

                // 缓存当前配置表
                s_DataTables.TryAdd(typeNamePair, dataTable);

                // 执行Hook回调
                ExecuteLoadedHooks(dataTable);
            }

            // 回调加载完毕
            onCompleted?.Invoke();
        }


        /// <summary>
        /// 执行Hook回调
        /// </summary>
        /// <param name="dataTable">数据表实例</param>
        private static void ExecuteLoadedHooks(DataTableBase dataTable)
        {
            var type = dataTable.GetType();

            // 执行特定类型的Hook
            if (s_TypedHooks.TryGetValue(type, out var typedHooks))
            {
                foreach (var hook in typedHooks)
                {
                    try
                    {
                        hook(dataTable);
                    }
                    catch (Exception ex)
                    {
                        // 记录但不中断加载过程
                        Console.WriteLine($"Typed hook failed for {type.Name}: {ex.Message}");
                    }
                }
            }

            // 执行全局Hook
            foreach (var hook in s_GlobalHooks)
            {
                try
                {
                    hook(dataTable);
                }
                catch (Exception ex)
                {
                    // 记录但不中断加载过程
                    Console.WriteLine($"Global hook failed for {type.Name}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 销毁数据表。
        /// </summary>
        /// <typeparam name="T">数据表行的类型。</typeparam>
        /// <param name="name">数据表名称。</param>
        public static bool DestroyDataTable<T>(string name = "") where T : DataTableBase
        {
            return InternalDestroyDataTable(new TypeNamePair(typeof(T), name));
        }

        /// <summary>
        /// 销毁数据表。
        /// </summary>
        /// <param name="dataTableType">数据表行的类型。</param>
        /// <param name="name">数据表名称。</param>
        /// <returns>是否销毁数据表成功。</returns>
        public static bool DestroyDataTable(Type dataTableType, string name = "")
        {
            if (dataTableType == null)
            {
                throw new Exception("Data table type is invalid.");
            }

            if (!dataTableType.IsSubclassOf(typeof(DataTableBase)))
            {
                throw new Exception($"Data table type '{dataTableType.FullName}' is invalid.");
            }

            return InternalDestroyDataTable(new TypeNamePair(dataTableType, name));
        }

        /// <summary>
        /// 销毁数据表。
        /// </summary>
        /// <param name="dataTable">要销毁的数据表。</param>
        /// <returns>是否销毁数据表成功。</returns>
        public static bool DestroyDataTable(DataTableBase dataTable)
        {
            if (dataTable == null)
            {
                throw new Exception("Data table is invalid.");
            }

            lock (s_Lock)
            {
                foreach (var pair in s_DataTables)
                {
                    if (pair.Value != dataTable) continue;
                    dataTable.Shutdown();
                    return s_DataTables.TryRemove(pair.Key, out var _);
                }

                return false;
            }
        }

        /// <summary>
        /// 内部方法：供生成的DTXXX类使用的获取数据表方法
        /// </summary>
        /// <typeparam name="T">数据表类型</typeparam>
        /// <returns>数据表实例</returns>
        public static T? GetDataTableInternal<T>() where T : DataTableBase
        {
            var dataTable = InternalGetDataTable(new TypeNamePair(typeof(T), string.Empty));
            return dataTable as T;
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
                    dataTable.Shutdown();
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }
    }
}
