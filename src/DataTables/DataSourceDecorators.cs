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

        public virtual ValueTask<bool> IsAvailableAsync() => Inner.IsAvailableAsync();
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
            foreach (var source in _sources)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (!await source.ExistsAsync(name, cancellationToken))
                    {
                        continue;
                    }

                    return await source.LoadAsync(name, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    lastError = ex;
                }
            }

            throw new FileNotFoundException($"所有数据源都无法加载: {name}", lastError);
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
                    entries.TryAdd(entry.Name, entry);
                }
            }

            return new DataSourceManifest(entries.Values.ToArray());
        }

        public async ValueTask<bool> IsAvailableAsync()
        {
            foreach (var source in _sources)
            {
                if (await source.IsAvailableAsync())
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

        public VersionedDataSource(IDataSource inner, string version, Func<string, string, string>? nameResolver = null) : base(inner)
        {
            _version = version ?? throw new ArgumentNullException(nameof(version));
            _nameResolver = nameResolver ?? ((name, version) => $"{version}/{name}");
        }

        public override ValueTask<byte[]> LoadAsync(string name, CancellationToken cancellationToken) => Inner.LoadAsync(_nameResolver(name, _version), cancellationToken);

        public override ValueTask<bool> ExistsAsync(string name, CancellationToken cancellationToken) => Inner.ExistsAsync(_nameResolver(name, _version), cancellationToken);

        public override async ValueTask<DataSourceManifest> GetManifestAsync(CancellationToken cancellationToken)
        {
            var manifest = await Inner.GetManifestAsync(cancellationToken);
            return new DataSourceManifest(manifest.Entries, _version);
        }
    }
}
