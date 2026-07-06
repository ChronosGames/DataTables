using System;
using System.Threading;
using System.Threading.Tasks;

namespace DataTables
{
    /// <summary>
    /// Unity Addressables 数据源适配预留基类。
    /// 具体项目可继承后桥接 Addressables.LoadAssetAsync<TextAsset>() 等 Unity API。
    /// </summary>
    public abstract class AddressablesDataSourceBase : IDataSource
    {
        public DataSourceType SourceType => DataSourceType.Addressables;

        public ValueTask<byte[]> LoadAsync(string tableName) => LoadAsync(tableName, CancellationToken.None);

        public abstract ValueTask<byte[]> LoadAsync(string name, CancellationToken cancellationToken);

        public virtual ValueTask<bool> ExistsAsync(string name, CancellationToken cancellationToken) => new ValueTask<bool>(true);

        public virtual ValueTask<DataSourceManifest> GetManifestAsync(CancellationToken cancellationToken) => new ValueTask<DataSourceManifest>(DataSourceManifest.Empty);

        public virtual ValueTask<bool> IsAvailableAsync() => new ValueTask<bool>(true);
    }

    /// <summary>
    /// Unity StreamingAssets 数据源适配预留类型。
    /// 默认复用文件系统读取；在 Android 等平台可继承并改用 UnityWebRequest。
    /// </summary>
    public class StreamingAssetsDataSource : FileSystemDataSource
    {
        public StreamingAssetsDataSource(string streamingAssetsPath) : base(streamingAssetsPath)
        {
        }

        public override DataSourceType SourceType => DataSourceType.StreamingAssets;
    }
}
