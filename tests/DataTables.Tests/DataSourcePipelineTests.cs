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

        var bytes = await fallback.LoadAsync("Config", CancellationToken.None);
        var manifest = await fallback.GetManifestAsync(CancellationToken.None);

        bytes.Should().Equal(1, 2, 3);
        fallback.LastHitSource.Should().Be("Memory:backup");
        manifest.Entries.Single().SourceName.Should().Be("Memory:primary");
        manifest.Entries.Single().Version.Should().Be("old", "first source wins for fallback manifest entries");
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
}
