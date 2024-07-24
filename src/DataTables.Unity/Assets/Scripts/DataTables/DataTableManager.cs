using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace DataTables
{
    public sealed partial class DataTableManager : IDataTableManager
    {
        private readonly ConcurrentDictionary<TypeNamePair, DataTableBase> m_DataTables;
        private IDataTableHelper? m_DataTableHelper;

        /// <summary>
        /// 初始化数据表管理器的新实例。
        /// </summary>
        public DataTableManager()
        {
            m_DataTables = new ConcurrentDictionary<TypeNamePair, DataTableBase>();
            m_DataTableHelper = null;
        }

        /// <summary>
        /// 获取数据表数量。
        /// </summary>
        public int Count => m_DataTables.Count;

        /// <summary>
        /// 设置数据表辅助器。
        /// </summary>
        /// <param name="dataTableHelper">数据表辅助器。</param>
        public void SetDataTableHelper(IDataTableHelper dataTableHelper)
        {
            m_DataTableHelper = dataTableHelper ?? throw new Exception("Data table helper is invalid.");
        }

        /// <summary>
        /// 关闭并清理数据表管理器。
        /// </summary>
        public void Shutdown()
        {
            foreach (var dataTable in m_DataTables)
            {
                dataTable.Value.Shutdown();
            }

            m_DataTables.Clear();
        }

        /// <summary>
        /// 是否存在数据表。
        /// </summary>
        /// <typeparam name="T">数据表行的类型。</typeparam>
        /// <param name="name">数据表名称。</param>
        /// <returns>是否存在数据表。</returns>
        public bool HasDataTable<T>(string name = "") where T : DataTableBase
        {
            return InternalHasDataTable(new TypeNamePair(typeof(T), name));
        }

        /// <summary>
        /// 是否存在数据表。
        /// </summary>
        /// <param name="dataTableType">数据表的类型。</param>
        /// <param name="name">数据表名称。</param>
        /// <returns>是否存在数据表。</returns>
        public bool HasDataTable(Type dataTableType, string name = "")
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
        public T? GetDataTable<T>(string name = "") where T : DataTableBase
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
        public DataTableBase? GetDataTable(Type dataTableType, string name = "")
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
        public DataTableBase[] GetAllDataTables()
        {
            int index = 0;
            DataTableBase[] results = new DataTableBase[m_DataTables.Count];
            foreach (var dataTable in m_DataTables)
            {
                results[index++] = dataTable.Value;
            }

            return results;
        }

        /// <summary>
        /// 获取所有数据表。
        /// </summary>
        /// <param name="results">所有数据表。</param>
        public void GetAllDataTables(List<DataTableBase> results)
        {
            if (results == null)
            {
                throw new Exception("Results is invalid.");
            }

            results.Clear();
            foreach (var dataTable in m_DataTables)
            {
                results.Add(dataTable.Value);
            }
        }

        /// <summary>
        /// 创建数据表。
        /// </summary>
        /// <typeparam name="T">数据表的类型。</typeparam>
        /// <param name="onCompleted">数据表加载完成时回调。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CreateDataTable<T>(Action onCompleted) where T : DataTableBase
        {
            CreateDataTable<T>(string.Empty, onCompleted);
        }

        /// <summary>
        /// 创建数据表。
        /// </summary>
        /// <param name="dataTableType">数据表的类型。</param>
        /// <param name="onCompleted">数据表加载完成时回调。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CreateDataTable(Type dataTableType, Action onCompleted)
        {
            CreateDataTable(dataTableType, string.Empty, onCompleted);
        }

        /// <summary>
        /// 创建数据表。
        /// </summary>
        /// <typeparam name="T">数据表的类型。</typeparam>
        /// <param name="name">数据表名称。</param>
        /// <param name="onCompleted">数据表加载完成时回调。</param>
        public void CreateDataTable<T>(string name, Action onCompleted) where T : DataTableBase
        {
            if (m_DataTableHelper == null)
            {
                throw new Exception("YOU MUST SetDataTableHelper FIRST.");
            }

            var typeNamePair = new TypeNamePair(typeof(T), name);
            if (InternalHasDataTable(typeNamePair))
            {
                throw new Exception($"Already exist data table '{typeNamePair}'.");
            }

            m_DataTableHelper.Read(typeNamePair.ToString(), (raw) => LoadDataTable(typeNamePair, raw, onCompleted));
        }

        /// <summary>
        /// 创建数据表。
        /// </summary>
        /// <param name="dataTableType">数据表的类型。</param>
        /// <param name="name">数据表名称。</param>
        /// <param name="onCompleted">数据表加载完成时回调。</param>
        public void CreateDataTable(Type dataTableType, string name, Action onCompleted)
        {
            if (!dataTableType.IsSubclassOf(typeof(DataTableBase)))
            {
                throw new Exception($"Data table type '{dataTableType.FullName}' is invalid.");
            }

            if (m_DataTableHelper == null)
            {
                throw new Exception("MUST SetDataTableHelper FIRST.");
            }

            var typeNamePair = new TypeNamePair(dataTableType, name);
            if (InternalHasDataTable(typeNamePair))
            {
                throw new Exception($"Already exist data table '{typeNamePair}'.");
            }

            m_DataTableHelper.Read(typeNamePair.ToString(), (raw) => LoadDataTable(typeNamePair, raw, onCompleted));
        }

        private void LoadDataTable(TypeNamePair typeNamePair, byte[] raw, Action? onCompleted)
        {
            if (InternalHasDataTable(typeNamePair))
            {
                throw new Exception($"Already exist data table '{typeNamePair}'.");
            }

            DataTableBase dataTable;
            using (var ms = new MemoryStream(raw, false))
            {
                using (var reader = new BinaryReader(ms, Encoding.UTF8))
                {
                    var rowCount = reader.ReadInt32();
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
            m_DataTables.TryAdd(typeNamePair, dataTable);

            // 回调加载完毕
            onCompleted?.Invoke();
        }

        /// <summary>
        /// 销毁数据表。
        /// </summary>
        /// <typeparam name="T">数据表行的类型。</typeparam>
        /// <param name="name">数据表名称。</param>
        public bool DestroyDataTable<T>(string name = "") where T : DataTableBase
        {
            return InternalDestroyDataTable(new TypeNamePair(typeof(T), name));
        }

        /// <summary>
        /// 销毁数据表。
        /// </summary>
        /// <param name="dataTableType">数据表行的类型。</param>
        /// <param name="name">数据表名称。</param>
        /// <returns>是否销毁数据表成功。</returns>
        public bool DestroyDataTable(Type dataTableType, string name = "")
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
        public bool DestroyDataTable(DataTableBase dataTable)
        {
            if (dataTable == null)
            {
                throw new Exception("Data table is invalid.");
            }

            foreach (var pair in m_DataTables)
            {
                if (pair.Value != dataTable) continue;
                dataTable.Shutdown();
                return m_DataTables.TryRemove(pair.Key, out var _);
            }

            return false;
        }

        private bool InternalHasDataTable(TypeNamePair pair)
        {
            return m_DataTables.ContainsKey(pair);
        }

        private DataTableBase? InternalGetDataTable(TypeNamePair pair) => m_DataTables.GetValueOrDefault(pair);

        private bool InternalDestroyDataTable(TypeNamePair pair)
        {
            if (m_DataTables.TryRemove(pair, out var dataTable))
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
