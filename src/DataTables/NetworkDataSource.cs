using System;
using System.Net.Http;
using System.Threading;
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

        public ValueTask<byte[]> LoadAsync(string tableName) => LoadAsync(tableName, CancellationToken.None);

        public async ValueTask<byte[]> LoadAsync(string name, CancellationToken cancellationToken)
        {
            var url = $"{_baseUrl}/{name}.bytes";
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
#if NET5_0_OR_GREATER
            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
#else
            cancellationToken.ThrowIfCancellationRequested();
            var bytes = await response.Content.ReadAsByteArrayAsync();
            cancellationToken.ThrowIfCancellationRequested();
            return bytes;
#endif
        }

        public async ValueTask<bool> ExistsAsync(string name, CancellationToken cancellationToken)
        {
            var url = $"{_baseUrl}/{name}.bytes";
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            return response.IsSuccessStatusCode;
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