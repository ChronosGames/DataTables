using System;
using System.Collections.Generic;
using System.IO;
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
        /// <param name="name">数据表或资源名称。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>由调用方负责释放的可读数据流。</returns>
        ValueTask<Stream> OpenReadAsync(string name, CancellationToken cancellationToken);

        /// <summary>
        /// 检查指定数据表是否存在。
        /// </summary>
        /// <param name="name">数据表或资源名称。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>是否存在。</returns>
        ValueTask<bool> ExistsAsync(string name, CancellationToken cancellationToken);

        /// <summary>
        /// 获取数据源清单。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>数据源清单。</returns>
        ValueTask<DataSourceManifest> GetManifestAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 检查数据源是否可用。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>是否可用。</returns>
        ValueTask<bool> IsAvailableAsync(CancellationToken cancellationToken);

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

    public static class DataSourceExtensions
    {
        public static ValueTask<byte[]> LoadAsync(this IDataSource source, string name)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return LoadAsync(source, name, CancellationToken.None);
        }

        public static async ValueTask<byte[]> LoadAsync(this IDataSource source, string name, CancellationToken cancellationToken)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            await using var input = await source.OpenReadAsync(name, cancellationToken);
            await using var output = new MemoryStream();
            await input.CopyToAsync(output, 81920, cancellationToken);
            return output.ToArray();
        }

        public static ValueTask<bool> ExistsAsync(this IDataSource source, string name)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.ExistsAsync(name, CancellationToken.None);
        }

        public static ValueTask<DataSourceManifest> GetManifestAsync(this IDataSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.GetManifestAsync(CancellationToken.None);
        }

        public static ValueTask<bool> IsAvailableAsync(this IDataSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.IsAvailableAsync(CancellationToken.None);
        }
    }
}
