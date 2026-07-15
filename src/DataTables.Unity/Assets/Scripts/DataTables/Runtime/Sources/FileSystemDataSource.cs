using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataTables
{
    /// <summary>
    /// 文件系统数据源实现
    /// </summary>
    public class FileSystemDataSource : IDataSource
    {
        private readonly string _dataDirectory;
        private readonly string _manifestResourceName;

        public virtual DataSourceType SourceType => DataSourceType.FileSystem;

        public FileSystemDataSource(string dataDirectory, string manifestResourceName = DataSourceManifestJson.FileName)
        {
            _dataDirectory = dataDirectory ?? throw new ArgumentNullException(nameof(dataDirectory));
            _manifestResourceName = string.IsNullOrWhiteSpace(manifestResourceName)
                ? throw new ArgumentException("Manifest resource name is required.", nameof(manifestResourceName))
                : manifestResourceName;
        }

        public ValueTask<Stream> OpenReadAsync(string name, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var filePath = GetFilePath(name);
            try
            {
                if (!File.Exists(filePath))
                {
                    var exception = new FileNotFoundException($"数据表文件不存在: {filePath}", filePath);
                    throw CreateException(DataSourceOperation.OpenRead, name, filePath, exception);
                }

                Stream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
                return new ValueTask<Stream>(stream);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (DataSourceException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw CreateException(DataSourceOperation.OpenRead, name, filePath, exception);
            }
        }

        public ValueTask<bool> ExistsAsync(string name, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var filePath = GetFilePath(name);
            try
            {
#if NET5_0_OR_GREATER
                return ValueTask.FromResult(File.Exists(filePath));
#else
                return new ValueTask<bool>(File.Exists(filePath));
#endif
            }
            catch (Exception exception)
            {
                throw CreateException(DataSourceOperation.Exists, name, filePath, exception);
            }
        }

        public async ValueTask<DataSourceManifest> GetManifestAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (!Directory.Exists(_dataDirectory))
                {
                    return DataSourceManifest.Empty;
                }

                var manifestPath = Path.Combine(_dataDirectory, _manifestResourceName);
                if (File.Exists(manifestPath))
                {
                    await using var input = new FileStream(manifestPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
                    await using var output = new MemoryStream();
                    await input.CopyToAsync(output, 81920, cancellationToken);
                    return DataSourceManifestJson.Parse(output.ToArray(), _dataDirectory);
                }

                var entries = Directory.EnumerateFiles(_dataDirectory, "*.bytes", SearchOption.AllDirectories)
                    .Select(path =>
                    {
                        var relative = Path.GetRelativePath(_dataDirectory, path);
                        var name = Path.ChangeExtension(relative, null).Replace(Path.DirectorySeparatorChar, '/');
                        var info = new FileInfo(path);
                        return new DataSourceManifestEntry(name, info.Length, sourceName: _dataDirectory);
                    })
                    .OrderBy(entry => entry.Name, StringComparer.Ordinal)
                    .ToArray();

                return new DataSourceManifest(entries);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (DataSourceException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw CreateException(
                    DataSourceOperation.GetManifest,
                    _manifestResourceName,
                    Path.Combine(_dataDirectory, _manifestResourceName),
                    exception);
            }
        }

        public ValueTask<bool> IsAvailableAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
#if NET5_0_OR_GREATER
                return ValueTask.FromResult(Directory.Exists(_dataDirectory));
#else
                return new ValueTask<bool>(Directory.Exists(_dataDirectory));
#endif
            }
            catch (Exception exception)
            {
                throw CreateException(DataSourceOperation.IsAvailable, _manifestResourceName, _dataDirectory, exception);
            }
        }

        private string GetFilePath(string name) => Path.Combine(_dataDirectory, $"{name}.bytes");

        private static DataSourceException CreateException(DataSourceOperation operation, string name, string location, Exception exception)
        {
            return new DataSourceException(
                DataSourceType.FileSystem,
                operation,
                name,
                location,
                Environment.OSVersion.Platform.ToString(),
                transportError: exception.Message,
                innerException: exception);
        }
    }
}
