using System;
using System.Threading.Tasks;

namespace DataTables
{
    /// <summary>
    /// 数据源接口
    /// </summary>
    public interface IDataSource
    {
        /// <summary>
        /// 异步加载数据表数据
        /// </summary>
        /// <param name="tableName">数据表名称</param>
        /// <returns>数据表的字节数据</returns>
        ValueTask<byte[]> LoadAsync(string tableName);

        /// <summary>
        /// 检查数据源是否可用
        /// </summary>
        /// <returns>是否可用</returns>
        ValueTask<bool> IsAvailableAsync();

        /// <summary>
        /// 数据源类型
        /// </summary>
        DataSourceType SourceType { get; }
    }

    /// <summary>
    /// 数据源类型
    /// </summary>
    public enum DataSourceType
    {
        FileSystem,         // 文件系统
        Resources,          // Unity Resources
        AssetBundle,        // Unity AssetBundle  
        StreamingAssets,    // Unity StreamingAssets
        Network,            // 网络数据源
        Memory              // 内存数据源
    }
}