using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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

        public abstract ValueTask<Stream> OpenReadAsync(string name, CancellationToken cancellationToken);

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

        public override async ValueTask<Stream> OpenReadAsync(string name, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_cache.TryGetValue(name, out var cached))
            {
                return new MemoryStream(cached, writable: false);
            }

            var bytes = await Inner.LoadAsync(name, cancellationToken);
            _cache[name] = bytes;
            return new MemoryStream(bytes, writable: false);
        }
    }

    public sealed class CompressedDataSource : DataSourceDecorator
    {
        public CompressedDataSource(IDataSource inner) : base(inner)
        {
        }

        public override async ValueTask<Stream> OpenReadAsync(string name, CancellationToken cancellationToken)
        {
            var input = await Inner.OpenReadAsync(name, cancellationToken);
            return new DeflateStream(input, CompressionMode.Decompress, leaveOpen: false);
        }
    }

    public sealed class EncryptedDataSource : DataSourceDecorator
    {
        private readonly Func<string, byte[], CancellationToken, ValueTask<byte[]>> _decryptAsync;
        private readonly byte[]? _aesKey;
        private readonly byte[]? _aesIv;

        public EncryptedDataSource(IDataSource inner, Func<string, byte[], CancellationToken, ValueTask<byte[]>> decryptAsync) : base(inner)
        {
            _decryptAsync = decryptAsync ?? throw new ArgumentNullException(nameof(decryptAsync));
            _aesKey = null;
            _aesIv = null;
        }

        public EncryptedDataSource(IDataSource inner, byte[] aesKey, byte[] aesIv) : base(inner)
        {
            _aesKey = (byte[])(aesKey ?? throw new ArgumentNullException(nameof(aesKey))).Clone();
            _aesIv = (byte[])(aesIv ?? throw new ArgumentNullException(nameof(aesIv))).Clone();
            _decryptAsync = null!;
        }

        public override async ValueTask<Stream> OpenReadAsync(string name, CancellationToken cancellationToken)
        {
            if (_aesKey != null)
            {
                var input = await Inner.OpenReadAsync(name, cancellationToken);
                try
                {
                    var aes = Aes.Create();
                    aes.Key = _aesKey;
                    aes.IV = _aesIv!;
                    var crypto = new CryptoStream(input, aes.CreateDecryptor(), CryptoStreamMode.Read);
                    return new OwnedReadStream(crypto, aes);
                }
                catch
                {
                    input.Dispose();
                    throw;
                }
            }

            var encrypted = await Inner.LoadAsync(name, cancellationToken);
            return new MemoryStream(await _decryptAsync(name, encrypted, cancellationToken), writable: false);
        }
    }


    public sealed class HashValidatedDataSource : DataSourceDecorator
    {
        public const string Algorithm = "SHA-256";
        public const string HashFormat = "hex-lowercase";
        private const int Sha256HexLength = 64;

        private readonly IReadOnlyDictionary<string, DataSourceManifestEntry> _entries;

        public HashValidatedDataSource(IDataSource inner, DataSourceManifest manifest) : base(inner)
        {
            if (manifest == null)
            {
                throw new ArgumentNullException(nameof(manifest));
            }

            _entries = manifest.Entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Hash))
                .ToDictionary(entry => entry.Name, StringComparer.OrdinalIgnoreCase);
        }

        public HashValidatedDataSource(IDataSource inner, IEnumerable<DataSourceManifestEntry> entries) : base(inner)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            _entries = entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Hash))
                .ToDictionary(entry => entry.Name, StringComparer.OrdinalIgnoreCase);
        }

        public override async ValueTask<Stream> OpenReadAsync(string name, CancellationToken cancellationToken)
        {
            var stream = await Inner.OpenReadAsync(name, cancellationToken);
            if (!_entries.TryGetValue(name, out var entry))
            {
                return stream;
            }

            try
            {
                ValidateHashFormat(entry);
                return new HashValidatingReadStream(stream, entry.Hash!, name, entry.SourceName ?? Inner.ToString() ?? Inner.GetType().Name);
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }

        private static void ValidateHashFormat(DataSourceManifestEntry entry)
        {
            var hash = entry.Hash!;
            if (hash.Length != Sha256HexLength || hash.Any(ch => !IsLowerHex(ch)))
            {
                throw new InvalidDataException($"Manifest hash for '{entry.Name}' must be {Algorithm} {HashFormat} ({Sha256HexLength} chars). Actual: '{hash}'.");
            }
        }

        private static bool IsLowerHex(char ch) => (ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f');

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

        public async ValueTask<Stream> OpenReadAsync(string name, CancellationToken cancellationToken)
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

                    var stream = await source.OpenReadAsync(name, cancellationToken);
                    LastHitSource = sourceName;
                    Log.Info($"FallbackDataSource loaded '{name}' from {sourceName}.");
                    return stream;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    var failedSourceName = GetSourceName(source);
                    lastError = ex;
                    failures.Add($"{failedSourceName}: {ex.Message}");
                    if (ex is InvalidDataException && ex.Message.IndexOf("hash", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Log.Warning($"FallbackDataSource hash validation failed for '{name}' from {failedSourceName}: {ex.Message}");
                    }
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

        public override ValueTask<Stream> OpenReadAsync(string name, CancellationToken cancellationToken) => Inner.OpenReadAsync(_nameResolver(name, _version), cancellationToken);

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
