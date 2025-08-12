using System;
using System.Collections.Concurrent;

namespace DataTables
{
    /// <summary>
    /// 数据表工厂基接口 - 支持非泛型调用
    /// </summary>
    public interface IDataTableFactory
    {
        /// <summary>
        /// 创建数据表实例
        /// </summary>
        /// <param name="name">表名</param>
        /// <param name="capacity">容量</param>
        /// <returns>数据表实例</returns>
        DataTableBase CreateTable(string name, int capacity);

        /// <summary>
        /// 创建数据行实例
        /// </summary>
        /// <returns>数据行实例</returns>
        DataRowBase CreateRow();
    }

    /// <summary>
    /// 数据表工厂接口 - 消除反射调用的性能瓶颈
    /// </summary>
    /// <typeparam name="TTable">数据表类型</typeparam>
    /// <typeparam name="TRow">数据行类型</typeparam>
    public interface IDataTableFactory<out TTable, out TRow> : IDataTableFactory
        where TTable : DataTableBase
        where TRow : DataRowBase
    {
        /// <summary>
        /// 创建数据表实例
        /// </summary>
        /// <param name="name">表名</param>
        /// <param name="capacity">容量</param>
        /// <returns>数据表实例</returns>
        new TTable CreateTable(string name, int capacity);

        /// <summary>
        /// 创建数据行实例
        /// </summary>
        /// <returns>数据行实例</returns>
        new TRow CreateRow();

        // 显式实现基接口
        DataTableBase IDataTableFactory.CreateTable(string name, int capacity) => CreateTable(name, capacity);
        DataRowBase IDataTableFactory.CreateRow() => CreateRow();
    }

    /// <summary>
    /// 数据表工厂管理器
    /// </summary>
    public static class DataTableFactoryManager
    {
        private static readonly ConcurrentDictionary<Type, IDataTableFactory> s_Factories = new();

        /// <summary>
        /// 注册数据表工厂
        /// </summary>
        /// <typeparam name="TTable">数据表类型</typeparam>
        /// <typeparam name="TRow">数据行类型</typeparam>
        /// <typeparam name="TFactory">工厂类型</typeparam>
        public static void RegisterFactory<TTable, TRow, TFactory>()
            where TTable : DataTableBase
            where TRow : DataRowBase
            where TFactory : IDataTableFactory<TTable, TRow>, new()
        {
            var factory = new TFactory();
            s_Factories[typeof(TTable)] = factory;
        }

        /// <summary>
        /// 获取数据表工厂
        /// </summary>
        /// <typeparam name="TTable">数据表类型</typeparam>
        /// <typeparam name="TRow">数据行类型</typeparam>
        /// <returns>工厂实例</returns>
        public static IDataTableFactory<TTable, TRow>? GetFactory<TTable, TRow>()
            where TTable : DataTableBase
            where TRow : DataRowBase
        {
            return s_Factories.TryGetValue(typeof(TTable), out IDataTableFactory? factory)
                ? factory as IDataTableFactory<TTable, TRow>
                : null;
        }

        /// <summary>
        /// 检查工厂是否已注册
        /// </summary>
        /// <typeparam name="TTable">数据表类型</typeparam>
        /// <returns>是否已注册</returns>
        public static bool HasFactory<TTable>() where TTable : DataTableBase
        {
            return s_Factories.ContainsKey(typeof(TTable));
        }

        public static DataTableBase? CreateDataTable(Type tableType, string name, int capacity)
        {
            if (s_Factories.TryGetValue(tableType, out var factory))
            {
                return factory.CreateTable(name, capacity);
            }

            // throw new InvalidOperationException($"No factory registered for type {tableType.FullName}");
            return null;
        }

        public static DataRowBase? CreateDataRow(Type tableType)
        {
            if (s_Factories.TryGetValue(tableType, out var factory))
            {
                return factory.CreateRow();
            }

            // throw new InvalidOperationException($"No factory registered for type {tableType.FullName}");
            return null;
        }
    }
}
