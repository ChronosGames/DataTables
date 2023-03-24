using System;
using System.Collections.Generic;

namespace DataTables
{
    /// <summary>
    /// 数据表管理器接口。
    /// </summary>
    public interface IDataTableManager
    {
        /// <summary>
        /// 获取数据表数量。
        /// </summary>
        int Count
        {
            get;
        }

        /// <summary>
        /// 是否存在数据表。
        /// </summary>
        /// <typeparam name="T">数据表行的类型。</typeparam>
        /// <returns>是否存在数据表。</returns>
        bool HasDataTable<T>() where T : DataTableBase;

        /// <summary>
        /// 是否存在数据表。
        /// </summary>
        /// <param name="dataTableType">数据表的类型。</param>
        /// <returns>是否存在数据表。</returns>
        bool HasDataTable(Type dataTableType);

        /// <summary>
        /// 获取数据表。
        /// </summary>
        /// <typeparam name="T">数据表的类型。</typeparam>
        /// <returns>要获取的数据表。</returns>
        T GetDataTable<T>() where T : DataTableBase;

        /// <summary>
        /// 获取数据表。
        /// </summary>
        /// <param name="dataTableType">数据表的类型。</param>
        /// <returns>要获取的数据表。</returns>
        DataTableBase GetDataTable(Type dataTableType);

        /// <summary>
        /// 获取所有数据表。
        /// </summary>
        /// <returns>所有数据表。</returns>
        DataTableBase[] GetAllDataTables();

        /// <summary>
        /// 获取所有数据表。
        /// </summary>
        /// <param name="results">所有数据表。</param>
        void GetAllDataTables(List<DataTableBase> results);

        /// <summary>
        /// 创建数据表。
        /// </summary>
        /// <typeparam name="T">数据表行的类型。</typeparam>
        /// <param name="raw">加载配置文件的字节流</param>
        /// <param name="offset">加载配置文件的起始字节索引</param>
        /// <param name="length">加载配置文件的字节长度</param>
        /// <returns>要创建的数据表。</returns>
        T CreateDataTable<T>(byte[] raw, int offset, int length) where T : DataTableBase, new();

        /// <summary>
        /// 销毁数据表。
        /// </summary>
        /// <typeparam name="T">数据表的类型。</typeparam>
        /// <returns>是否销毁数据表成功。</returns>
        bool DestroyDataTable<T>() where T : DataTableBase;

        /// <summary>
        /// 销毁数据表。
        /// </summary>
        /// <param name="dataTableType">数据表行的类型。</param>
        /// <returns>是否销毁数据表成功。</returns>
        bool DestroyDataTable(Type dataTableType);

        /// <summary>
        /// 销毁数据表。
        /// </summary>
        /// <param name="dataTable">要销毁的数据表。</param>
        /// <returns>是否销毁数据表成功。</returns>
        bool DestroyDataTable(DataTableBase dataTable);
    }
}
