using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DataTables
{
    /// <summary>
    /// 数据源接口。
    /// </summary>
    public interface IDataSource
    {
        /// <summary>
        /// 异步加载数据表数据。
        /// </summary>
        /// <param name="tableName">数据表名称。</param>
        /// <returns>数据表的字节数据。</returns>
        ValueTask<byte[]> LoadAsync(string tableName)
            => LoadAsync(tableName, CancellationToken.None);

        /// <summary>
        /// 异步加载数据表数据。
        /// </summary>
        /// <param name="name">数据表或资源名称。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>数据表的字节数据。</returns>
        ValueTask<byte[]> LoadAsync(string name, CancellationToken cancellationToken);

        /// <summary>
        /// 检查指定数据表是否存在。
        /// </summary>
        /// <param name="name">数据表或资源名称。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>是否存在。</returns>
        ValueTask<bool> ExistsAsync(string name, CancellationToken cancellationToken)
            => new ValueTask<bool>(true);

        /// <summary>
        /// 获取数据源清单。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>数据源清单。</returns>
        ValueTask<DataSourceManifest> GetManifestAsync(CancellationToken cancellationToken)
            => new ValueTask<DataSourceManifest>(DataSourceManifest.Empty);

        /// <summary>
        /// 检查数据源是否可用。
        /// </summary>
        /// <returns>是否可用。</returns>
        ValueTask<bool> IsAvailableAsync()
            => IsAvailableAsync(CancellationToken.None);

        /// <summary>
        /// 检查数据源是否可用。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>是否可用。</returns>
        ValueTask<bool> IsAvailableAsync(CancellationToken cancellationToken)
            => IsAvailableAsync();

        /// <summary>
        /// 数据源类型。
        /// </summary>
        DataSourceType SourceType { get; }
    }

    /// <summary>
    /// 数据源清单。
    /// </summary>
    public sealed class DataSourceManifest
    {
        public static readonly DataSourceManifest Empty = new DataSourceManifest(Array.Empty<DataSourceManifestEntry>());

        public DataSourceManifest(IReadOnlyList<DataSourceManifestEntry> entries, string? version = null)
        {
            Entries = entries ?? throw new ArgumentNullException(nameof(entries));
            Version = version;
        }

        public IReadOnlyList<DataSourceManifestEntry> Entries { get; }

        public string? Version { get; }
    }

    /// <summary>
    /// 数据源清单条目。
    /// </summary>
    public sealed class DataSourceManifestEntry
    {
        public DataSourceManifestEntry(string name, long? length = null, string? version = null, string? hash = null, string? sourceName = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Length = length;
            Version = version;
            Hash = hash;
            SourceName = sourceName;
        }

        public string Name { get; }

        public long? Length { get; }

        public string? Version { get; }

        /// <summary>
        /// 资源内容 hash。约定为解码后的 DataTables payload 的 SHA-256 小写 hex（64 字符）。
        /// </summary>
        public string? Hash { get; }

        /// <summary>
        /// 提供该条目的数据源名称，用于 fallback/manifest 诊断。
        /// </summary>
        public string? SourceName { get; }
    }

    /// <summary>
    /// 数据源类型。
    /// </summary>
    public enum DataSourceType
    {
        FileSystem,         // 文件系统
        Resources,          // Unity Resources
        AssetBundle,        // Unity AssetBundle
        Addressables,       // Unity Addressables
        StreamingAssets,    // Unity StreamingAssets
        Network,            // 网络数据源
        Memory,             // 内存数据源
        Composite           // 组合数据源
    }
}
