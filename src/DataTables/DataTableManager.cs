using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DataTables
{
    public sealed partial class DataTableManager : IDataTableManager
    {
        private readonly Dictionary<Type, DataTableBase> m_DataTables;

        /// <summary>
        /// 初始化数据表管理器的新实例。
        /// </summary>
        public DataTableManager()
        {
            m_DataTables = new Dictionary<Type, DataTableBase>();
        }

        /// <summary>
        /// 获取数据表数量。
        /// </summary>
        public int Count
        {
            get
            {
                return m_DataTables.Count;
            }
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
        /// <returns>是否存在数据表。</returns>
        public bool HasDataTable<T>() where T : DataTableBase
        {
            return InternalHasDataTable(typeof(T));
        }

        /// <summary>
        /// 是否存在数据表。
        /// </summary>
        /// <param name="dataTableType">数据表的类型。</param>
        /// <returns>是否存在数据表。</returns>
        public bool HasDataTable(Type dataTableType)
        {
            if (dataTableType == null)
            {
                throw new Exception("Data table type is invalid.");
            }

            if (!dataTableType.IsSubclassOf(typeof(DataTableBase)))
            {
                throw new Exception(string.Format("Data row type '{0}' is invalid.", dataTableType.FullName));
            }

            return InternalHasDataTable(dataTableType);
        }

        /// <summary>
        /// 获取数据表。
        /// </summary>
        /// <typeparam name="T">数据表的类型。</typeparam>
        /// <returns>要获取的数据表。</returns>
        public T GetDataTable<T>() where T : DataTableBase
        {
            return (T)InternalGetDataTable(typeof(T));
        }

        /// <summary>
        /// 获取数据表。
        /// </summary>
        /// <param name="dataTableType">数据表行的类型。</param>
        /// <returns>要获取的数据表。</returns>
        public DataTableBase GetDataTable(Type dataTableType)
        {
            if (dataTableType == null)
            {
                throw new Exception("Data table type is invalid.");
            }

            if (!dataTableType.IsSubclassOf(typeof(DataTableBase)))
            {
                throw new Exception(string.Format("Data table type '{0}' is invalid.", dataTableType.FullName));
            }

            return InternalGetDataTable(dataTableType);
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

        public T CreateDataTable<T>(byte[] raw, int offset, int length) where T : DataTableBase, new()
        {
            var dataTableType = typeof(T);
            if (InternalHasDataTable(dataTableType))
            {
                throw new Exception(string.Format("Already exist data table '{0}'.", dataTableType));
            }

            var dataTable = new T();
            using (var ms = new MemoryStream(raw, offset, length, false))
            {
                using (var reader = new BinaryReader(ms, Encoding.UTF8))
                {
                    while (reader.BaseStream.Position < reader.BaseStream.Length)
                    {
                        int dataRowBytesLength = reader.Read7BitEncodedInt32();
                        if (!dataTable.AddDataRow(reader))
                        {
                            return null;
                        }

                        //reader.BaseStream.Position += dataRowBytesLength;
                    }
                }
            }

            m_DataTables.Add(dataTableType, dataTable);
            return dataTable;
        }

        /// <summary>
        /// 销毁数据表。
        /// </summary>
        /// <typeparam name="T">数据表行的类型。</typeparam>
        public bool DestroyDataTable<T>() where T : DataTableBase
        {
            return InternalDestroyDataTable(typeof(T));
        }

        /// <summary>
        /// 销毁数据表。
        /// </summary>
        /// <param name="dataTableType">数据表行的类型。</param>
        /// <returns>是否销毁数据表成功。</returns>
        public bool DestroyDataTable(Type dataTableType)
        {
            if (dataTableType == null)
            {
                throw new Exception("Data table type is invalid.");
            }

            if (!dataTableType.IsSubclassOf(typeof(DataTableBase)))
            {
                throw new Exception(string.Format("Data table type '{0}' is invalid.", dataTableType.FullName));
            }

            return InternalDestroyDataTable(dataTableType);
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

            return InternalDestroyDataTable(dataTable.Type);
        }

        private bool InternalHasDataTable(Type dataTableType)
        {
            return m_DataTables.ContainsKey(dataTableType);
        }

        private DataTableBase InternalGetDataTable(Type dataTableType)
        {
            DataTableBase dataTable = null;
            if (m_DataTables.TryGetValue(dataTableType, out dataTable))
            {
                return dataTable;
            }

            return null;
        }

        private bool InternalDestroyDataTable(Type dataTableType)
        {
            DataTableBase dataTable = null;
            if (m_DataTables.TryGetValue(dataTableType, out dataTable))
            {
                dataTable.Shutdown();
                return m_DataTables.Remove(dataTableType);
            }

            return false;
        }
    }
}
