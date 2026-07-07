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
        private static readonly TimeSpan DefaultAvailabilityTimeout = TimeSpan.FromSeconds(5);
        private readonly string _baseUrl;
        private readonly string? _availabilityResourceName;
        private readonly TimeSpan _availabilityTimeout;
        private readonly HttpClient _httpClient;
        private static readonly HttpClient s_sharedHttpClient = new();

        public NetworkDataSource(string baseUrl)
            : this(baseUrl, availabilityResourceName: "manifest.json")
        {
        }

        public NetworkDataSource(string baseUrl, string? availabilityResourceName, TimeSpan? availabilityTimeout = null, HttpClient? httpClient = null)
        {
            _baseUrl = baseUrl?.TrimEnd('/') ?? throw new ArgumentNullException(nameof(baseUrl));
            _availabilityResourceName = string.IsNullOrWhiteSpace(availabilityResourceName) ? null : availabilityResourceName.TrimStart('/');
            _availabilityTimeout = availabilityTimeout ?? DefaultAvailabilityTimeout;
            _httpClient = httpClient ?? s_sharedHttpClient;
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

        public ValueTask<bool> IsAvailableAsync() => IsAvailableAsync(CancellationToken.None);

        public async ValueTask<bool> IsAvailableAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(_availabilityTimeout);
                var token = timeoutCts.Token;
                var url = GetAvailabilityUrl();

                using var headRequest = new HttpRequestMessage(HttpMethod.Head, url);
                using var headResponse = await _httpClient.SendAsync(headRequest, HttpCompletionOption.ResponseHeadersRead, token);
                if (headResponse.IsSuccessStatusCode)
                {
                    return true;
                }

                using var getResponse = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
                return getResponse.IsSuccessStatusCode;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                return false;
            }
        }

        private string GetAvailabilityUrl()
        {
            return _availabilityResourceName == null ? _baseUrl : $"{_baseUrl}/{_availabilityResourceName}";
        }
    }
}
