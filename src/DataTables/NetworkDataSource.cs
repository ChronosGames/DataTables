using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace DataTables
{
    /// <summary>
    /// 网络数据源示例 - 演示如何实现自定义数据源
    /// </summary>
    public class NetworkDataSource : IDataSource
    {
        private readonly string _baseUrl;
        private static readonly HttpClient _httpClient = new();

        public NetworkDataSource(string baseUrl)
        {
            _baseUrl = baseUrl?.TrimEnd('/') ?? throw new ArgumentNullException(nameof(baseUrl));
        }

        public DataSourceType SourceType => DataSourceType.Network;

        public async ValueTask<byte[]> LoadAsync(string tableName)
        {
            var url = $"{_baseUrl}/{tableName}.bytes";
            return await _httpClient.GetByteArrayAsync(url);
        }

        public async ValueTask<bool> IsAvailableAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync(_baseUrl, HttpCompletionOption.ResponseHeadersRead);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}