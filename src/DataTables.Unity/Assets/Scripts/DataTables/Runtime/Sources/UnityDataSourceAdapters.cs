using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
#if UNITY_5_3_OR_NEWER
using UnityEngine;
#endif
#if UNITY_ANDROID || UNITY_WEBGL
using UnityEngine.Networking;
#endif

namespace DataTables
{
    /// <summary>
    /// Unity Addressables 数据源适配预留基类。
    /// 具体项目可继承后桥接 Addressables.LoadAssetAsync&lt;TextAsset&gt;() 等 Unity API。
    /// </summary>
    public abstract class AddressablesDataSourceBase : IDataSource
    {
        public DataSourceType SourceType => DataSourceType.Addressables;

        public abstract ValueTask<Stream> OpenReadAsync(string name, CancellationToken cancellationToken);

        public virtual ValueTask<bool> ExistsAsync(string name, CancellationToken cancellationToken) => new ValueTask<bool>(true);

        public virtual ValueTask<DataSourceManifest> GetManifestAsync(CancellationToken cancellationToken) => new ValueTask<DataSourceManifest>(DataSourceManifest.Empty);

        public virtual ValueTask<bool> IsAvailableAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<bool>(true);
        }
    }

    /// <summary>
    /// Unity StreamingAssets 数据源。
    /// Android 与 WebGL 使用 UnityWebRequest，其余平台使用异步文件流。
    /// </summary>
    public sealed class StreamingAssetsDataSource : IDataSource
    {
        private readonly string m_StreamingAssetsPath;
        private readonly string m_ManifestResourceName;
        private readonly FileSystemDataSource m_FileSystem;

#if UNITY_5_3_OR_NEWER
        public StreamingAssetsDataSource() : this(Application.streamingAssetsPath)
        {
        }
#endif

        public StreamingAssetsDataSource(string streamingAssetsPath, string manifestResourceName = DataSourceManifestJson.FileName)
        {
            if (streamingAssetsPath == null) throw new ArgumentNullException(nameof(streamingAssetsPath));
            if (string.IsNullOrWhiteSpace(streamingAssetsPath)) throw new ArgumentException("StreamingAssets path is required.", nameof(streamingAssetsPath));
            m_StreamingAssetsPath = streamingAssetsPath.TrimEnd('/', '\\');
            m_ManifestResourceName = string.IsNullOrWhiteSpace(manifestResourceName)
                ? throw new ArgumentException("Manifest resource name is required.", nameof(manifestResourceName))
                : manifestResourceName.TrimStart('/', '\\');
            _ = GetResourceLocation(m_ManifestResourceName);
            m_FileSystem = new FileSystemDataSource(m_StreamingAssetsPath, m_ManifestResourceName);
        }

        public DataSourceType SourceType => DataSourceType.StreamingAssets;

        public async ValueTask<Stream> OpenReadAsync(string name, CancellationToken cancellationToken)
        {
            var location = GetResourceLocation(name + ".bytes");
#if UNITY_ANDROID || UNITY_WEBGL
            var bytes = await DownloadBytesAsync(DataSourceOperation.OpenRead, name, location, cancellationToken);
            return new MemoryStream(bytes, writable: false);
#else
            try
            {
                return await m_FileSystem.OpenReadAsync(name, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw CreateException(DataSourceOperation.OpenRead, name, location, exception);
            }
#endif
        }

        public async ValueTask<bool> ExistsAsync(string name, CancellationToken cancellationToken)
        {
#if UNITY_ANDROID || UNITY_WEBGL
            var manifest = await GetManifestAsync(cancellationToken);
            return manifest.Entries.Any(entry => string.Equals(entry.Name, name, StringComparison.Ordinal));
#else
            try
            {
                return await m_FileSystem.ExistsAsync(name, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw CreateException(DataSourceOperation.Exists, name, GetResourceLocation(name + ".bytes"), exception);
            }
#endif
        }

        public async ValueTask<DataSourceManifest> GetManifestAsync(CancellationToken cancellationToken)
        {
            var location = GetResourceLocation(m_ManifestResourceName);
#if UNITY_ANDROID || UNITY_WEBGL
            var bytes = await DownloadBytesAsync(DataSourceOperation.GetManifest, m_ManifestResourceName, location, cancellationToken);
            try
            {
                return DataSourceManifestJson.Parse(bytes, m_StreamingAssetsPath);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw CreateException(DataSourceOperation.GetManifest, m_ManifestResourceName, location, exception);
            }
#else
            try
            {
                return await m_FileSystem.GetManifestAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw CreateException(DataSourceOperation.GetManifest, m_ManifestResourceName, location, exception);
            }
#endif
        }

        public async ValueTask<bool> IsAvailableAsync(CancellationToken cancellationToken)
        {
#if UNITY_ANDROID || UNITY_WEBGL
            try
            {
                await GetManifestAsync(cancellationToken);
                return true;
            }
            catch (DataSourceException exception) when (exception.HttpStatusCode == 404)
            {
                return false;
            }
#else
            try
            {
                if (!await m_FileSystem.IsAvailableAsync(cancellationToken)) return false;
                await m_FileSystem.GetManifestAsync(cancellationToken);
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw CreateException(DataSourceOperation.IsAvailable, m_ManifestResourceName, GetResourceLocation(m_ManifestResourceName), exception);
            }
#endif
        }

        internal string GetResourceLocation(string relativePath)
        {
            var segments = relativePath.Replace('\\', '/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0 || segments.Any(segment => segment == "." || segment == ".."))
            {
                throw new ArgumentException("Resource path must contain safe path segments.", nameof(relativePath));
            }
            var escapedPath = string.Join("/", segments.Select(Uri.EscapeDataString));
            return m_StreamingAssetsPath.Replace('\\', '/').TrimEnd('/') + "/" + escapedPath;
        }

#if UNITY_ANDROID || UNITY_WEBGL
        private async ValueTask<byte[]> DownloadBytesAsync(DataSourceOperation operation, string name, string location, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var request = UnityWebRequest.Get(location);
            var completion = new TaskCompletionSource<bool>();
            var asyncOperation = request.SendWebRequest();
            void Complete(AsyncOperation _) => completion.TrySetResult(true);
            asyncOperation.completed += Complete;
            if (asyncOperation.isDone) completion.TrySetResult(true);
            using var registration = cancellationToken.Register(() =>
            {
                request.Abort();
                completion.TrySetException(new OperationCanceledException(cancellationToken));
            });

            try
            {
                await completion.Task;
                cancellationToken.ThrowIfCancellationRequested();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    var status = request.responseCode > 0 ? request.responseCode : (long?)null;
                    throw new DataSourceException(SourceType, operation, name, location, Application.platform.ToString(), status, request.error);
                }
                return request.downloadHandler.data;
            }
            finally
            {
                asyncOperation.completed -= Complete;
            }
        }
#endif

        private static DataSourceException CreateException(DataSourceOperation operation, string name, string location, Exception exception)
        {
#if UNITY_5_3_OR_NEWER
            var platform = Application.platform.ToString();
#else
            var platform = Environment.OSVersion.Platform.ToString();
#endif
            if (exception is DataSourceException dataSourceException
                && dataSourceException.SourceType == DataSourceType.StreamingAssets)
            {
                return dataSourceException;
            }

            return new DataSourceException(DataSourceType.StreamingAssets, operation, name, location, platform, transportError: exception.Message, innerException: exception);
        }

        public override string ToString() => m_StreamingAssetsPath;
    }
}
