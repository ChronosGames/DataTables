using System;
using System.Net;
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

        public async ValueTask<System.IO.Stream> OpenReadAsync(string name, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var url = AppendResourceUrl(name + ".bytes");
            HttpResponseMessage? response = null;
            try
            {
                response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    throw CreateException(DataSourceOperation.OpenRead, name, url, response);
                }
#if NET5_0_OR_GREATER
                var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
#else
                var stream = await response.Content.ReadAsStreamAsync();
#endif
                var owned = new OwnedReadStream(stream, response);
                response = null;
                return owned;
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
                throw CreateException(DataSourceOperation.OpenRead, name, url, exception);
            }
            finally
            {
                response?.Dispose();
            }
        }

        public async ValueTask<bool> ExistsAsync(string name, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var url = AppendResourceUrl(name + ".bytes");
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, url);
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (response.IsSuccessStatusCode) return true;
                if (response.StatusCode == HttpStatusCode.NotFound) return false;
                throw CreateException(DataSourceOperation.Exists, name, url, response);
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
                throw CreateException(DataSourceOperation.Exists, name, url, exception);
            }
        }

        public async ValueTask<DataSourceManifest> GetManifestAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_availabilityResourceName == null) return DataSourceManifest.Empty;

            var url = GetAvailabilityUrl();
            try
            {
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseContentRead, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    throw CreateException(DataSourceOperation.GetManifest, _availabilityResourceName, url, response);
                }
#if NET5_0_OR_GREATER
                var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
#else
                var bytes = await response.Content.ReadAsByteArrayAsync();
#endif
                return DataSourceManifestJson.Parse(bytes, _baseUrl);
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
                throw CreateException(DataSourceOperation.GetManifest, _availabilityResourceName, url, exception);
            }
        }

        public ValueTask<bool> IsAvailableAsync() => IsAvailableAsync(CancellationToken.None);

        public async ValueTask<bool> IsAvailableAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var url = GetAvailabilityUrl();
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(_availabilityTimeout);
                var token = timeoutCts.Token;

                using var headRequest = new HttpRequestMessage(HttpMethod.Head, url);
                using var headResponse = await _httpClient.SendAsync(headRequest, HttpCompletionOption.ResponseHeadersRead, token);
                if (headResponse.IsSuccessStatusCode)
                {
                    return true;
                }
                if (headResponse.StatusCode == HttpStatusCode.NotFound)
                {
                    return false;
                }

                using var getResponse = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
                if (getResponse.IsSuccessStatusCode) return true;
                if (getResponse.StatusCode == HttpStatusCode.NotFound) return false;
                throw CreateException(DataSourceOperation.IsAvailable, _availabilityResourceName ?? string.Empty, url, getResponse);
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
                throw CreateException(DataSourceOperation.IsAvailable, _availabilityResourceName ?? string.Empty, url, exception);
            }
        }

        private string GetAvailabilityUrl()
        {
            return _availabilityResourceName == null ? _baseUrl : AppendResourceUrl(_availabilityResourceName);
        }

        private string AppendResourceUrl(string resourceName)
        {
            var segments = resourceName.Replace('\\', '/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0 || Array.Exists(segments, segment => segment == "." || segment == ".."))
            {
                throw new ArgumentException("Resource name must contain safe path segments.", nameof(resourceName));
            }

            return _baseUrl + "/" + string.Join("/", Array.ConvertAll(segments, Uri.EscapeDataString));
        }

        private DataSourceException CreateException(DataSourceOperation operation, string? name, string location, HttpResponseMessage response)
        {
            return new DataSourceException(
                SourceType,
                operation,
                name ?? string.Empty,
                location,
                Environment.OSVersion.Platform.ToString(),
                (long)response.StatusCode,
                response.ReasonPhrase);
        }

        private DataSourceException CreateException(DataSourceOperation operation, string? name, string location, Exception exception)
        {
            return new DataSourceException(
                SourceType,
                operation,
                name ?? string.Empty,
                location,
                Environment.OSVersion.Platform.ToString(),
                transportError: exception.Message,
                innerException: exception);
        }
    }
}
