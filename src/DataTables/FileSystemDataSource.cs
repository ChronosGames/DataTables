using System;
using System.IO;
using System.Threading.Tasks;

namespace DataTables
{
    /// <summary>
    /// 文件系统数据源实现
    /// </summary>
    public class FileSystemDataSource : IDataSource
    {
        private readonly string _dataDirectory;

        public DataSourceType SourceType => DataSourceType.FileSystem;

        public FileSystemDataSource(string dataDirectory)
        {
            _dataDirectory = dataDirectory ?? throw new ArgumentNullException(nameof(dataDirectory));
        }

        public async ValueTask<byte[]> LoadAsync(string tableName)
        {
            var filePath = Path.Combine(_dataDirectory, $"{tableName}.bytes");

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"数据表文件不存在: {filePath}");
            }

            return await File.ReadAllBytesAsync(filePath);
        }

        public ValueTask<bool> IsAvailableAsync()
        {
#if NET5_0_OR_GREATER
            return ValueTask.FromResult(Directory.Exists(_dataDirectory));
#else
            return new ValueTask<bool>(Directory.Exists(_dataDirectory));
#endif
        }
    }
}
