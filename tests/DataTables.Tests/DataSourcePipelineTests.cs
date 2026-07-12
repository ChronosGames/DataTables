using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using DataTables;
using FluentAssertions;
using Xunit;

namespace DataTables.Tests;

public class DataSourcePipelineTests
{
    [Fact]
    public void IDataSource_ShouldExposeOneCanonicalCancelableContract()
    {
        var methods = typeof(IDataSource).GetMethods();

        methods.Where(method => method.Name == nameof(IDataSource.OpenReadAsync)).Should().ContainSingle();
        methods.Where(method => method.Name == nameof(IDataSource.IsAvailableAsync)).Should().ContainSingle();
        methods.Should().NotContain(method => method.Name == "LoadAsync");
        methods.Single(method => method.Name == nameof(IDataSource.OpenReadAsync)).IsAbstract.Should().BeTrue();
        methods.Single(method => method.Name == nameof(IDataSource.ExistsAsync)).IsAbstract.Should().BeTrue();
        methods.Single(method => method.Name == nameof(IDataSource.IsAvailableAsync)).IsAbstract.Should().BeTrue();
    }

    [Fact]
    public async Task FallbackDataSource_Should_Record_Hit_Source_And_Manifest_Source()
    {
        var first = new StubDataSource("primary", false, new DataSourceManifestEntry("Config", 10, "old", "hash-old"));
        var second = new StubDataSource("backup", true, new DataSourceManifestEntry("Config", 12, "new", "hash-new"));
        var fallback = new FallbackDataSource(first, second);

        await using var stream = await fallback.OpenReadAsync("Config", CancellationToken.None);
        var manifest = await fallback.GetManifestAsync(CancellationToken.None);

        var buffered = stream.Should().BeOfType<MemoryStream>().Subject;
        buffered.CanSeek.Should().BeTrue();
        buffered.ReadByte().Should().Be(1);
        buffered.Position = 0;
        buffered.ReadByte().Should().Be(1);
        buffered.ToArray().Should().Equal(1, 2, 3);
        fallback.LastHitSource.Should().Be("Memory:backup");
        manifest.Entries.Single().SourceName.Should().Be("Memory:primary");
        manifest.Entries.Single().Version.Should().Be("old", "first source wins for fallback manifest entries");
        manifest.Entries.Single().Hash.Should().BeNull("different source payloads cannot share one fallback-wide hash");
    }

    [Fact]
    public async Task FallbackDataSource_ManifestShouldNotExposeHashUnlessEverySourceDeclaresTheSameHash()
    {
        const string hash = "039058c6f2c0cb492c533b0a4d14ef77cc0f78abccced5287d84a1a2011cfb81";
        var fallback = new FallbackDataSource(
            new StubDataSource("primary", true, new DataSourceManifestEntry("Config", hash: hash)),
            new StubDataSource("backup", true));
        var matching = new FallbackDataSource(
            new StubDataSource("primary", true, new DataSourceManifestEntry("Config", hash: hash)),
            new StubDataSource("backup", true, new DataSourceManifestEntry("Config", hash: hash)));

        var manifest = await fallback.GetManifestAsync(CancellationToken.None);
        var matchingManifest = await matching.GetManifestAsync(CancellationToken.None);

        manifest.Entries.Single().Hash.Should().BeNull("a backup without a declared hash may serve different content");
        matchingManifest.Entries.Single().Hash.Should().Be(hash, "a common source hash is safe to expose");
    }

    [Fact]
    public async Task FallbackDataSource_ShouldFallbackWhenCandidateStreamFailsDuringRead()
    {
        var failedStream = new ThrowAfterReadStream(new byte[] { 9, 9, 9 }, bytesBeforeFailure: 1);
        var fallback = new FallbackDataSource(
            new StreamDataSource("primary", () => failedStream),
            new StubDataSource("backup", true));

        var payload = await fallback.LoadAsync("Config", CancellationToken.None);

        payload.Should().Equal(1, 2, 3);
        failedStream.Disposed.Should().BeTrue();
        fallback.LastHitSource.Should().Be("Memory:backup");
    }

    [Fact]
    public async Task FallbackDataSource_ShouldFallbackWhenDecompressionFailsDuringRead()
    {
        var corruptCompressed = new CompressedDataSource(
            new StreamDataSource("primary", () => new MemoryStream(new byte[] { 0x07 }, writable: false)));
        var fallback = new FallbackDataSource(corruptCompressed, new StubDataSource("backup", true));

        var payload = await fallback.LoadAsync("Config", CancellationToken.None);

        payload.Should().Equal(1, 2, 3);
        fallback.LastHitSource.Should().Be("Memory:backup");
    }

    [Fact]
    public async Task FallbackDataSource_ShouldFallbackWhenDecryptionFailsDuringRead()
    {
        using var aes = Aes.Create();
        var encoded = new MemoryStream();
        await using (var encryptor = new CryptoStream(encoded, aes.CreateEncryptor(), CryptoStreamMode.Write, leaveOpen: true))
        {
            await encryptor.WriteAsync(new byte[] { 9, 9, 9 });
        }

        var truncated = encoded.ToArray()[..^1];
        var corruptEncrypted = new EncryptedDataSource(new PayloadDataSource(truncated), aes.Key, aes.IV);
        var fallback = new FallbackDataSource(corruptEncrypted, new StubDataSource("backup", true));

        var payload = await fallback.LoadAsync("Config", CancellationToken.None);

        payload.Should().Equal(1, 2, 3);
        fallback.LastHitSource.Should().Be("Memory:backup");
    }

    [Fact]
    public async Task FallbackDataSource_ShouldValidateEachSourceWithItsOwnHashBeforeRecordingHit()
    {
        var primaryInner = new ManifestPayloadDataSource(
            "primary",
            new byte[] { 9 },
            new DataSourceManifestEntry("Config", hash: Sha256Hex(new byte[] { 1 })));
        var backupPayload = new byte[] { 4, 5, 6 };
        var backupInner = new ManifestPayloadDataSource(
            "backup",
            backupPayload,
            new DataSourceManifestEntry("Config", hash: Sha256Hex(backupPayload)));
        var primary = new HashValidatedDataSource(primaryInner, await primaryInner.GetManifestAsync(CancellationToken.None));
        var backup = new HashValidatedDataSource(backupInner, await backupInner.GetManifestAsync(CancellationToken.None));
        var fallback = new FallbackDataSource(primary, backup);

        var logs = new List<(Log.Level Level, string Message)>();
        byte[] payload;
        Log.Configure((level, message, _) => logs.Add((level, message)));
        try
        {
            payload = await fallback.LoadAsync("Config", CancellationToken.None);
        }
        finally
        {
            Log.Configure(null!);
        }
        var manifest = await fallback.GetManifestAsync(CancellationToken.None);

        payload.Should().Equal(backupPayload);
        primaryInner.OpenCount.Should().Be(1);
        backupInner.OpenCount.Should().Be(1);
        fallback.LastHitSource.Should().Be("Memory:backup");
        manifest.Entries.Single().Hash.Should().BeNull("the primary and backup have source-specific hashes");
        logs.Should().ContainSingle(entry => entry.Level == Log.Level.Info && entry.Message.Contains("Memory:backup"));
        logs.Should().NotContain(entry => entry.Level == Log.Level.Info && entry.Message.Contains("Memory:primary"),
            "a source must not log success before its delayed hash validation completes");
    }

    [Fact]
    public async Task FallbackDataSource_Should_Report_Attempted_Sources_When_All_Fail()
    {
        var fallback = new FallbackDataSource(
            new StubDataSource("primary", false),
            new StubDataSource("backup", true, throwOnLoad: true));

        var act = async () => await fallback.LoadAsync("Missing", CancellationToken.None);

        var ex = await act.Should().ThrowAsync<FileNotFoundException>();
        ex.Which.Message.Should().Contain("尝试结果");
        ex.Which.Message.Should().Contain("not found");
        ex.Which.Message.Should().Contain("backup load failed");
    }

    [Fact]
    public async Task HashValidatedDataSource_Should_Verify_Decoded_Payload_Before_Returning()
    {
        var manifest = new DataSourceManifest(new[]
        {
            new DataSourceManifestEntry("Config", hash: "039058c6f2c0cb492c533b0a4d14ef77cc0f78abccced5287d84a1a2011cfb81")
        });
        var source = new HashValidatedDataSource(new StubDataSource("cdn", true), manifest);

        var bytes = await source.LoadAsync("Config", CancellationToken.None);

        bytes.Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task HashValidatedDataSource_Should_Reject_Mismatch_And_Uppercase_Hash()
    {
        var mismatch = new HashValidatedDataSource(
            new StubDataSource("cdn", true),
            new[] { new DataSourceManifestEntry("Config", hash: "139058c6f2c0cb492c533b0a4d14ef77cc0f78abccced5287d84a1a2011cfb81") });
        var uppercase = new HashValidatedDataSource(
            new StubDataSource("cdn", true),
            new[] { new DataSourceManifestEntry("Config", hash: "039058C6F2C0CB492C533B0A4D14EF77CC0F78ABCCCED5287D84A1A2011CFB81") });

        var mismatchAct = async () => await mismatch.LoadAsync("Config", CancellationToken.None);
        var uppercaseAct = async () => await uppercase.LoadAsync("Config", CancellationToken.None);

        await mismatchAct.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*Hash validation failed*");
        await uppercaseAct.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*hex-lowercase*");
    }

    [Fact]
    public async Task HashValidatedDataSource_ZeroLengthReadShouldNotBeTreatedAsEndOfStream()
    {
        var payload = new byte[] { 1, 2, 3 };
        var source = new HashValidatedDataSource(
            new PayloadDataSource(payload),
            new[] { new DataSourceManifestEntry("Config", hash: Sha256Hex(payload)) });

        await using var stream = await source.OpenReadAsync("Config", CancellationToken.None);
        stream.Read(Array.Empty<byte>(), 0, 0).Should().Be(0);
        await using var output = new MemoryStream();
        await stream.CopyToAsync(output);

        output.ToArray().Should().Equal(payload);
    }

    [Fact]
    public async Task VersionedDataSource_Should_Filter_And_Map_Manifest_To_Logical_Names()
    {
        var inner = new StubDataSource(
            "cdn",
            true,
            new DataSourceManifestEntry("v2/Hero", 20, hash: "hero-hash"),
            new DataSourceManifestEntry("v1/Hero", 10, hash: "old-hash"),
            new DataSourceManifestEntry("v2/Item", 30, version: "v2.1", hash: "item-hash"));
        var versioned = new VersionedDataSource(inner, "v2");

        var manifest = await versioned.GetManifestAsync(CancellationToken.None);

        manifest.Version.Should().Be("v2");
        manifest.Entries.Select(entry => entry.Name).Should().BeEquivalentTo("Hero", "Item");
        manifest.Entries.Single(entry => entry.Name == "Hero").Version.Should().Be("v2");
        manifest.Entries.Single(entry => entry.Name == "Hero").Hash.Should().Be("hero-hash");
        manifest.Entries.Single(entry => entry.Name == "Item").Version.Should().Be("v2.1");
    }

    [Fact]
    public async Task NetworkDataSource_IsAvailableAsync_Should_Probe_Manifest_With_Head_Then_Get_Fallback()
    {
        var handler = new RecordingHandler(request => request.Method == HttpMethod.Head
            ? new HttpResponseMessage(HttpStatusCode.MethodNotAllowed)
            : new HttpResponseMessage(HttpStatusCode.OK));
        var source = new NetworkDataSource(
            "https://cdn.example.com/data/",
            "health.txt",
            TimeSpan.FromSeconds(1),
            new HttpClient(handler));

        var available = await source.IsAvailableAsync(CancellationToken.None);

        available.Should().BeTrue();
        handler.Requests.Select(request => (request.Method, request.RequestUri!.ToString())).Should().Equal(
            (HttpMethod.Head, "https://cdn.example.com/data/health.txt"),
            (HttpMethod.Get, "https://cdn.example.com/data/health.txt"));
    }

    [Fact]
    public async Task NetworkDataSource_IsAvailableAsync_Should_Respect_CancellationToken()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var source = new NetworkDataSource(
            "https://cdn.example.com/data",
            "manifest.json",
            TimeSpan.FromSeconds(1),
            new HttpClient(handler));

        var act = async () => await source.IsAvailableAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task FileSystemDataSource_ShouldExposeAFileStream()
    {
        var directory = Path.Combine(Path.GetTempPath(), "dt_stream_source_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        await File.WriteAllBytesAsync(Path.Combine(directory, "Config.bytes"), new byte[] { 1, 2, 3 });
        var source = new FileSystemDataSource(directory);

        await using var stream = await source.OpenReadAsync("Config", CancellationToken.None);

        stream.Should().BeOfType<FileStream>();
        stream.CanSeek.Should().BeTrue();
    }

    [Fact]
    public async Task NetworkDataSource_StreamShouldOwnHttpResponseLifetime()
    {
        var payload = new TrackingMemoryStream(new byte[] { 1, 2, 3 });
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(payload)
        });
        var source = new NetworkDataSource("https://cdn.example.com/data", "manifest.json", httpClient: new HttpClient(handler));

        var stream = await source.OpenReadAsync("Config", CancellationToken.None);
        payload.Disposed.Should().BeFalse();
        await stream.DisposeAsync();

        payload.Disposed.Should().BeTrue();
    }

    [Fact]
    public async Task CachedDataSource_ShouldOpenInnerOnceAndReturnIndependentStreams()
    {
        var inner = new PayloadDataSource(new byte[] { 1, 2, 3 });
        var source = new CachedDataSource(inner);

        await using var first = await source.OpenReadAsync("Config", CancellationToken.None);
        await using var second = await source.OpenReadAsync("Config", CancellationToken.None);
        first.ReadByte().Should().Be(1);
        second.ReadByte().Should().Be(1);

        inner.OpenCount.Should().Be(1);
        source.MaxCacheBytes.Should().Be(CachedDataSource.DefaultMaxCacheBytes);
        source.CachedBytes.Should().Be(3);
        source.Count.Should().Be(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CachedDataSource_ShouldRejectNonPositiveByteBudgets(long maxCacheBytes)
    {
        var action = () => new CachedDataSource(new PayloadDataSource(new byte[] { 1 }), maxCacheBytes);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task CachedDataSource_ShouldEvictLeastRecentlyUsedPayloadWithinByteBudget()
    {
        var inner = new PayloadDataSource(new byte[] { 1, 2 });
        var source = new CachedDataSource(inner, maxCacheBytes: 3);

        await source.LoadAsync("First", CancellationToken.None);
        await source.LoadAsync("Second", CancellationToken.None);
        await source.LoadAsync("First", CancellationToken.None);

        inner.OpenCount.Should().Be(3, "First should have been evicted when Second was cached");
        source.CachedBytes.Should().Be(2);
        source.Count.Should().Be(1);
    }

    [Fact]
    public async Task CachedDataSource_ShouldNotCacheSinglePayloadLargerThanByteBudget()
    {
        var inner = new PayloadDataSource(new byte[] { 1, 2, 3 });
        var source = new CachedDataSource(inner, maxCacheBytes: 2);

        await source.LoadAsync("Config", CancellationToken.None);
        await source.LoadAsync("Config", CancellationToken.None);

        inner.OpenCount.Should().Be(2);
        source.CachedBytes.Should().Be(0);
        source.Count.Should().Be(0);
    }

    [Fact]
    public async Task CompressedDataSource_ShouldDecodeWhileReading()
    {
        var encoded = new MemoryStream();
        await using (var compressor = new DeflateStream(encoded, CompressionLevel.Fastest, leaveOpen: true))
        {
            await compressor.WriteAsync(new byte[] { 1, 2, 3 });
        }

        var source = new CompressedDataSource(new PayloadDataSource(encoded.ToArray()));

        (await source.LoadAsync("Config", CancellationToken.None)).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task EncryptedDataSource_ShouldDecodeAesWhileReading()
    {
        using var aes = Aes.Create();
        var encoded = new MemoryStream();
        await using (var encryptor = new CryptoStream(encoded, aes.CreateEncryptor(), CryptoStreamMode.Write, leaveOpen: true))
        {
            await encryptor.WriteAsync(new byte[] { 1, 2, 3 });
        }

        var source = new EncryptedDataSource(new PayloadDataSource(encoded.ToArray()), aes.Key, aes.IV);

        (await source.LoadAsync("Config", CancellationToken.None)).Should().Equal(1, 2, 3);
    }

    private sealed class StubDataSource : IDataSource
    {
        private readonly string _name;
        private readonly bool _exists;
        private readonly bool _throwOnLoad;
        private readonly DataSourceManifest _manifest;

        public StubDataSource(string name, bool exists, params DataSourceManifestEntry[] entries)
            : this(name, exists, false, entries)
        {
        }

        public StubDataSource(string name, bool exists, bool throwOnLoad, params DataSourceManifestEntry[] entries)
        {
            _name = name;
            _exists = exists;
            _throwOnLoad = throwOnLoad;
            _manifest = new DataSourceManifest(entries, name);
        }

        public DataSourceType SourceType => DataSourceType.Memory;

        public ValueTask<Stream> OpenReadAsync(string name, CancellationToken cancellationToken)
        {
            if (_throwOnLoad)
            {
                throw new InvalidOperationException($"{_name} load failed");
            }

            return new ValueTask<Stream>(new MemoryStream(new byte[] { 1, 2, 3 }, writable: false));
        }

        public ValueTask<bool> ExistsAsync(string name, CancellationToken cancellationToken) => new ValueTask<bool>(_exists);

        public ValueTask<DataSourceManifest> GetManifestAsync(CancellationToken cancellationToken) => new ValueTask<DataSourceManifest>(_manifest);

        public ValueTask<bool> IsAvailableAsync(CancellationToken cancellationToken) => new ValueTask<bool>(true);

        public override string ToString() => _name;
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Task.FromResult(_responseFactory(request));
        }
    }

    private sealed class TrackingMemoryStream : MemoryStream
    {
        public TrackingMemoryStream(byte[] bytes) : base(bytes, writable: false) { }

        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class PayloadDataSource : IDataSource
    {
        private readonly byte[] _payload;

        public PayloadDataSource(byte[] payload)
        {
            _payload = payload;
        }

        public int OpenCount { get; private set; }

        public DataSourceType SourceType => DataSourceType.Memory;

        public ValueTask<Stream> OpenReadAsync(string name, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenCount++;
            return ValueTask.FromResult<Stream>(new MemoryStream(_payload, writable: false));
        }

        public ValueTask<bool> ExistsAsync(string name, CancellationToken cancellationToken) => ValueTask.FromResult(true);

        public ValueTask<DataSourceManifest> GetManifestAsync(CancellationToken cancellationToken) => ValueTask.FromResult(DataSourceManifest.Empty);

        public ValueTask<bool> IsAvailableAsync(CancellationToken cancellationToken) => ValueTask.FromResult(true);
    }

    private sealed class ManifestPayloadDataSource : IDataSource
    {
        private readonly string _name;
        private readonly byte[] _payload;
        private readonly DataSourceManifest _manifest;

        public ManifestPayloadDataSource(string name, byte[] payload, params DataSourceManifestEntry[] entries)
        {
            _name = name;
            _payload = payload;
            _manifest = new DataSourceManifest(entries);
        }

        public int OpenCount { get; private set; }

        public DataSourceType SourceType => DataSourceType.Memory;

        public ValueTask<Stream> OpenReadAsync(string name, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenCount++;
            return ValueTask.FromResult<Stream>(new MemoryStream(_payload, writable: false));
        }

        public ValueTask<bool> ExistsAsync(string name, CancellationToken cancellationToken) => ValueTask.FromResult(true);

        public ValueTask<DataSourceManifest> GetManifestAsync(CancellationToken cancellationToken) => ValueTask.FromResult(_manifest);

        public ValueTask<bool> IsAvailableAsync(CancellationToken cancellationToken) => ValueTask.FromResult(true);

        public override string ToString() => _name;
    }

    private sealed class StreamDataSource : IDataSource
    {
        private readonly string _name;
        private readonly Func<Stream> _streamFactory;

        public StreamDataSource(string name, Func<Stream> streamFactory)
        {
            _name = name;
            _streamFactory = streamFactory;
        }

        public DataSourceType SourceType => DataSourceType.Memory;

        public ValueTask<Stream> OpenReadAsync(string name, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(_streamFactory());
        }

        public ValueTask<bool> ExistsAsync(string name, CancellationToken cancellationToken) => ValueTask.FromResult(true);

        public ValueTask<DataSourceManifest> GetManifestAsync(CancellationToken cancellationToken) => ValueTask.FromResult(DataSourceManifest.Empty);

        public ValueTask<bool> IsAvailableAsync(CancellationToken cancellationToken) => ValueTask.FromResult(true);

        public override string ToString() => _name;
    }

    private sealed class ThrowAfterReadStream : Stream
    {
        private readonly byte[] _payload;
        private readonly int _bytesBeforeFailure;
        private int _position;

        public ThrowAfterReadStream(byte[] payload, int bytesBeforeFailure)
        {
            _payload = payload;
            _bytesBeforeFailure = bytesBeforeFailure;
        }

        public bool Disposed { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _payload.Length;
        public override long Position { get => _position; set => throw new NotSupportedException(); }
        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position >= _bytesBeforeFailure) throw new IOException("read failed after partial payload");
            var read = Math.Min(count, _bytesBeforeFailure - _position);
            Array.Copy(_payload, _position, buffer, offset, read);
            _position += read;
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private static string Sha256Hex(byte[] payload) => Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
}
