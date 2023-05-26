﻿using System;
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
        /// 设置数据表辅助器。
        /// </summary>
        /// <param name="dataTableHelper">数据表辅助器。</param>
        void SetDataTableHelper(IDataTableHelper dataTableHelper);

        /// <summary>
        /// 是否存在数据表。
        /// </summary>
        /// <typeparam name="T">数据表的类型。</typeparam>
        /// <returns>是否存在数据表。</returns>
        bool HasDataTable<T>() where T : DataTableBase;

        /// <summary>
        /// 是否存在数据表。
        /// </summary>
        /// <param name="dataTableType">数据表的类型。</param>
        /// <returns>是否存在数据表。</returns>
        bool HasDataTable(Type dataTableType);

        /// <summary>
        /// 是否存在数据表。
        /// </summary>
        /// <typeparam name="T">数据表的类型。</typeparam>
        /// <param name="name">数据表名称。</param>
        /// <returns>是否存在数据表。</returns>
        bool HasDataTable<T>(string name) where T : DataTableBase;

        /// <summary>
        /// 是否存在数据表。
        /// </summary>
        /// <param name="dataTableType">数据表的类型。</param>
        /// <param name="name">数据表名称。</param>
        /// <returns>是否存在数据表。</returns>
        bool HasDataTable(Type dataTableType, string name);

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
        /// 获取数据表。
        /// </summary>
        /// <typeparam name="T">数据表的类型。</typeparam>
        /// <param name="name">数据表名称。</param>
        /// <returns>要获取的数据表。</returns>
        T GetDataTable<T>(string name) where T : DataTableBase;

        /// <summary>
        /// 获取数据表。
        /// </summary>
        /// <param name="dataTableType">数据表的类型。</param>
        /// <param name="name">数据表名称。</param>
        /// <returns>要获取的数据表。</returns>
        DataTableBase GetDataTable(Type dataTableType, string name);

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
        /// <typeparam name="T">数据表的类型。</typeparam>
        /// <returns>要创建的数据表。</returns>
        T CreateDataTable<T>() where T : DataTableBase;

        /// <summary>
        /// 创建数据表。
        /// </summary>
        /// <param name="dataTableType">数据表的类型。</param>
        /// <returns></returns>
        DataTableBase CreateDataTable(Type dataTableType);

        /// <summary>
        /// 创建数据表。
        /// </summary>
        /// <typeparam name="T">数据表的类型。</typeparam>
        /// <param name="name">数据表名称。</param>
        /// <returns>要创建的数据表。</returns>
        T CreateDataTable<T>(string name) where T : DataTableBase;

        /// <summary>
        /// 创建数据表。
        /// </summary>
        /// <param name="dataTableType">数据表的类型。</param>
        /// <param name="name">数据表名称。</param>
        /// <returns></returns>
        DataTableBase CreateDataTable(Type dataTableType, string name);

        /// <summary>
        /// 销毁数据表。
        /// </summary>
        /// <typeparam name="T">数据表的类型。</typeparam>
        /// <returns>是否销毁数据表成功。</returns>
        bool DestroyDataTable<T>() where T : DataTableBase;

        /// <summary>
        /// 销毁数据表。
        /// </summary>
        /// <param name="dataTableType">数据表的类型。</param>
        /// <returns>是否销毁数据表成功。</returns>
        bool DestroyDataTable(Type dataTableType);

        /// <summary>
        /// 销毁数据表。
        /// </summary>
        /// <param name="dataTable">要销毁的数据表。</param>
        /// <returns>是否销毁数据表成功。</returns>
        bool DestroyDataTable(DataTableBase dataTable);

        /// <summary>
        /// 销毁数据表。
        /// </summary>
        /// <typeparam name="T">数据表的类型。</typeparam>
        /// <param name="name">数据表名称。</param>
        /// <returns>是否销毁数据表成功。</returns>
        bool DestroyDataTable<T>(string name) where T : DataTableBase;

        /// <summary>
        /// 销毁数据表。
        /// </summary>
        /// <param name="dataTableType">数据表的类型。</param>
        /// <param name="name">数据表名称。</param>
        /// <returns>是否销毁数据表成功。</returns>
        bool DestroyDataTable(Type dataTableType, string name);
    }
}
