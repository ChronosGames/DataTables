using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace DataTables
{
    public abstract class DataSourceDecorator : IDataSource
    {
        protected DataSourceDecorator(IDataSource inner)
        {
            Inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        protected IDataSource Inner { get; }

        public virtual DataSourceType SourceType => Inner.SourceType;

        public virtual ValueTask<byte[]> LoadAsync(string name) => LoadAsync(name, CancellationToken.None);

        public abstract ValueTask<byte[]> LoadAsync(string name, CancellationToken cancellationToken);

        public virtual ValueTask<bool> ExistsAsync(string name, CancellationToken cancellationToken) => Inner.ExistsAsync(name, cancellationToken);

        public virtual ValueTask<DataSourceManifest> GetManifestAsync(CancellationToken cancellationToken) => Inner.GetManifestAsync(cancellationToken);

        public virtual ValueTask<bool> IsAvailableAsync() => IsAvailableAsync(CancellationToken.None);

        public virtual ValueTask<bool> IsAvailableAsync(CancellationToken cancellationToken) => Inner.IsAvailableAsync(cancellationToken);
    }

    public sealed class CachedDataSource : DataSourceDecorator
    {
        private readonly ConcurrentDictionary<string, byte[]> _cache = new();

        public CachedDataSource(IDataSource inner) : base(inner)
        {
        }

        public void Clear() => _cache.Clear();

        public override async ValueTask<byte[]> LoadAsync(string name, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_cache.TryGetValue(name, out var cached))
            {
                return cached;
            }

            var bytes = await Inner.LoadAsync(name, cancellationToken);
            _cache[name] = bytes;
            return bytes;
        }
    }

    public sealed class CompressedDataSource : DataSourceDecorator
    {
        public CompressedDataSource(IDataSource inner) : base(inner)
        {
        }

        public override async ValueTask<byte[]> LoadAsync(string name, CancellationToken cancellationToken)
        {
            var compressed = await Inner.LoadAsync(name, cancellationToken);
            await using var input = new MemoryStream(compressed, writable: false);
            await using var deflate = new DeflateStream(input, CompressionMode.Decompress);
            await using var output = new MemoryStream();
            await deflate.CopyToAsync(output, cancellationToken);
            return output.ToArray();
        }
    }

    public sealed class EncryptedDataSource : DataSourceDecorator
    {
        private readonly Func<string, byte[], CancellationToken, ValueTask<byte[]>> _decryptAsync;

        public EncryptedDataSource(IDataSource inner, Func<string, byte[], CancellationToken, ValueTask<byte[]>> decryptAsync) : base(inner)
        {
            _decryptAsync = decryptAsync ?? throw new ArgumentNullException(nameof(decryptAsync));
        }

        public EncryptedDataSource(IDataSource inner, byte[] aesKey, byte[] aesIv) : this(inner, (_, bytes, token) => DecryptAesAsync(bytes, aesKey, aesIv, token))
        {
        }

        public override async ValueTask<byte[]> LoadAsync(string name, CancellationToken cancellationToken)
        {
            var encrypted = await Inner.LoadAsync(name, cancellationToken);
            return await _decryptAsync(name, encrypted, cancellationToken);
        }

        private static async ValueTask<byte[]> DecryptAesAsync(byte[] encrypted, byte[] key, byte[] iv, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            await using var input = new MemoryStream(encrypted, writable: false);
            await using var crypto = new CryptoStream(input, aes.CreateDecryptor(), CryptoStreamMode.Read);
            await using var output = new MemoryStream();
            await crypto.CopyToAsync(output, cancellationToken);
            return output.ToArray();
        }
    }

    public sealed class FallbackDataSource : IDataSource
    {
        private readonly IReadOnlyList<IDataSource> _sources;

        public string? LastHitSource { get; private set; }

        public FallbackDataSource(params IDataSource[] sources) : this((IEnumerable<IDataSource>)sources)
        {
        }

        public FallbackDataSource(IEnumerable<IDataSource> sources)
        {
            _sources = (sources ?? throw new ArgumentNullException(nameof(sources))).ToArray();
            if (_sources.Count == 0)
            {
                throw new ArgumentException("至少需要一个数据源。", nameof(sources));
            }
        }

        public DataSourceType SourceType => DataSourceType.Composite;

        public ValueTask<byte[]> LoadAsync(string name) => LoadAsync(name, CancellationToken.None);

        public async ValueTask<byte[]> LoadAsync(string name, CancellationToken cancellationToken)
        {
            Exception? lastError = null;
            var failures = new List<string>();
            foreach (var source in _sources)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var sourceName = GetSourceName(source);
                    if (!await source.ExistsAsync(name, cancellationToken))
                    {
                        failures.Add($"{sourceName}: not found");
                        continue;
                    }

                    var bytes = await source.LoadAsync(name, cancellationToken);
                    LastHitSource = sourceName;
                    return bytes;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    lastError = ex;
                    failures.Add($"{GetSourceName(source)}: {ex.Message}");
                }
            }

            throw new FileNotFoundException($"所有数据源都无法加载: {name}. 尝试结果: {string.Join("; ", failures)}", lastError);
        }

        public async ValueTask<bool> ExistsAsync(string name, CancellationToken cancellationToken)
        {
            foreach (var source in _sources)
            {
                if (await source.ExistsAsync(name, cancellationToken))
                {
                    return true;
                }
            }

            return false;
        }

        public async ValueTask<DataSourceManifest> GetManifestAsync(CancellationToken cancellationToken)
        {
            var entries = new Dictionary<string, DataSourceManifestEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var source in _sources)
            {
                var manifest = await source.GetManifestAsync(cancellationToken);
                foreach (var entry in manifest.Entries)
                {
                    entries.TryAdd(entry.Name, new DataSourceManifestEntry(entry.Name, entry.Length, entry.Version ?? manifest.Version, entry.Hash, GetSourceName(source)));
                }
            }

            return new DataSourceManifest(entries.Values.ToArray());
        }

        private static string GetSourceName(IDataSource source)
        {
            var displayName = source.ToString();
            if (string.IsNullOrWhiteSpace(displayName) || displayName == source.GetType().FullName)
            {
                displayName = source.GetType().Name;
            }

            return $"{source.SourceType}:{displayName}";
        }

        public ValueTask<bool> IsAvailableAsync() => IsAvailableAsync(CancellationToken.None);

        public async ValueTask<bool> IsAvailableAsync(CancellationToken cancellationToken)
        {
            foreach (var source in _sources)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await source.IsAvailableAsync(cancellationToken))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public sealed class VersionedDataSource : DataSourceDecorator
    {
        private readonly string _version;
        private readonly Func<string, string, string> _nameResolver;
        private readonly Func<DataSourceManifestEntry, string, DataSourceManifestEntry?> _manifestEntryMapper;

        public VersionedDataSource(IDataSource inner, string version, Func<string, string, string>? nameResolver = null, Func<DataSourceManifestEntry, string, DataSourceManifestEntry?>? manifestEntryMapper = null) : base(inner)
        {
            _version = version ?? throw new ArgumentNullException(nameof(version));
            _nameResolver = nameResolver ?? ((name, version) => $"{version}/{name}");
            _manifestEntryMapper = manifestEntryMapper ?? MapDefaultVersionedEntry;
        }

        public override ValueTask<byte[]> LoadAsync(string name, CancellationToken cancellationToken) => Inner.LoadAsync(_nameResolver(name, _version), cancellationToken);

        public override ValueTask<bool> ExistsAsync(string name, CancellationToken cancellationToken) => Inner.ExistsAsync(_nameResolver(name, _version), cancellationToken);

        public override async ValueTask<DataSourceManifest> GetManifestAsync(CancellationToken cancellationToken)
        {
            var manifest = await Inner.GetManifestAsync(cancellationToken);
            var entries = manifest.Entries
                .Select(entry => _manifestEntryMapper(entry, _version))
                .Where(entry => entry != null)
                .Select(entry => entry!)
                .ToArray();
            return new DataSourceManifest(entries, _version);
        }

        private static DataSourceManifestEntry? MapDefaultVersionedEntry(DataSourceManifestEntry entry, string version)
        {
            var prefix = version.TrimEnd('/') + "/";
            if (!entry.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return new DataSourceManifestEntry(
                entry.Name.Substring(prefix.Length),
                entry.Length,
                entry.Version ?? version,
                entry.Hash,
                entry.SourceName);
        }
    }
}
