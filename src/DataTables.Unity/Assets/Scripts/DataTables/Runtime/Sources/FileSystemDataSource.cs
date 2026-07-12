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

        public virtual DataSourceType SourceType => DataSourceType.FileSystem;

        public FileSystemDataSource(string dataDirectory)
        {
            _dataDirectory = dataDirectory ?? throw new ArgumentNullException(nameof(dataDirectory));
        }

        public ValueTask<Stream> OpenReadAsync(string name, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var filePath = GetFilePath(name);

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"数据表文件不存在: {filePath}");
            }

            Stream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            return new ValueTask<Stream>(stream);
        }

        public ValueTask<bool> ExistsAsync(string name, CancellationToken cancellationToken)
        {
#if NET5_0_OR_GREATER
            return ValueTask.FromResult(File.Exists(GetFilePath(name)));
#else
            return new ValueTask<bool>(File.Exists(GetFilePath(name)));
#endif
        }

        public ValueTask<DataSourceManifest> GetManifestAsync(CancellationToken cancellationToken)
        {
            if (!Directory.Exists(_dataDirectory))
            {
                return new ValueTask<DataSourceManifest>(DataSourceManifest.Empty);
            }

            var entries = Directory.EnumerateFiles(_dataDirectory, "*.bytes", SearchOption.AllDirectories)
                .Select(path =>
                {
                    var relative = Path.GetRelativePath(_dataDirectory, path);
                    var name = Path.ChangeExtension(relative, null).Replace(Path.DirectorySeparatorChar, '/');
                    var info = new FileInfo(path);
                    return new DataSourceManifestEntry(name, info.Length);
                })
                .ToArray();

            return new ValueTask<DataSourceManifest>(new DataSourceManifest(entries));
        }

        public ValueTask<bool> IsAvailableAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
#if NET5_0_OR_GREATER
            return ValueTask.FromResult(Directory.Exists(_dataDirectory));
#else
            return new ValueTask<bool>(Directory.Exists(_dataDirectory));
#endif
        }

        private string GetFilePath(string name) => Path.Combine(_dataDirectory, $"{name}.bytes");
    }
}
