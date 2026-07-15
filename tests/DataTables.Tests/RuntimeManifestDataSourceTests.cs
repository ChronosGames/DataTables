using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DataTables.GeneratorCore;
using FluentAssertions;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using Xunit;

namespace DataTables.Tests;

public sealed class RuntimeManifestDataSourceTests
{
    private static readonly string s_HashA = new('a', 64);
    private static readonly string s_HashB = new('b', 64);

    [Fact]
    public async Task GeneratorManifest_ShouldBeDeterministicAndChangeOnlyTheModifiedEntryAndRootVersion()
    {
        var paths = CreatePaths();
        var workbookPath = Path.Combine(paths.Input, "tables.xlsx");
        await CreateWorkbookAsync(workbookPath, itemId: 1, otherId: 2);
        (await GenerateAsync(paths)).Succeeded.Should().BeTrue();
        var manifestPath = Path.Combine(paths.Data, "manifest.json");
        var firstBytes = await File.ReadAllBytesAsync(manifestPath);
        var first = ReadManifest(firstBytes);

        (await GenerateAsync(paths)).Succeeded.Should().BeTrue();
        (await File.ReadAllBytesAsync(manifestPath)).Should().Equal(firstBytes);
        AssertManifestMatchesOutputs(first, paths.Data);

        await CreateWorkbookAsync(workbookPath, itemId: 7, otherId: 2);
        (await GenerateAsync(paths)).Succeeded.Should().BeTrue();
        var second = ReadManifest(await File.ReadAllBytesAsync(manifestPath));

        second.Version.Should().NotBe(first.Version);
        second.Entries["DataTables.Tests.Manifest.DTItem"].Hash.Should().NotBe(first.Entries["DataTables.Tests.Manifest.DTItem"].Hash);
        second.Entries["DataTables.Tests.Manifest.DTOther"].Should().Be(first.Entries["DataTables.Tests.Manifest.DTOther"]);
        AssertManifestMatchesOutputs(second, paths.Data);
    }

    [Fact]
    public async Task DataOnly_ShouldWriteRuntimeManifestWhileValidateOnlyWritesNothing()
    {
        var dataOnly = CreatePaths();
        await CreateWorkbookAsync(Path.Combine(dataOnly.Input, "tables.xlsx"), itemId: 1, otherId: 2);

        var dataOnlyResult = await GenerateAsync(dataOnly, GenerationMode.DataOnly);

        dataOnlyResult.Succeeded.Should().BeTrue();
        File.Exists(Path.Combine(dataOnly.Data, "manifest.json")).Should().BeTrue();
        Directory.GetFiles(dataOnly.Code, "*", SearchOption.AllDirectories).Should().BeEmpty();

        var validate = CreatePaths();
        await CreateWorkbookAsync(Path.Combine(validate.Input, "tables.xlsx"), itemId: 1, otherId: 2);
        var validateResult = await GenerateAsync(validate, GenerationMode.ValidateOnly);

        validateResult.Succeeded.Should().BeTrue();
        Directory.GetFiles(validate.Code, "*", SearchOption.AllDirectories).Should().BeEmpty();
        Directory.GetFiles(validate.Data, "*", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Fact]
    public async Task FileSystemDataSource_ShouldPreferRuntimeManifestAndInjectActualSourceName()
    {
        var directory = CreateDirectory("dt_manifest_filesystem_");
        await File.WriteAllBytesAsync(Path.Combine(directory, "Ignored.bytes"), [1, 2, 3]);
        await File.WriteAllTextAsync(Path.Combine(directory, "manifest.json"), CreateManifestJson(
            version: s_HashA,
            new ManifestEntry("Declared", 42, s_HashB, s_HashB)));
        var source = new global::DataTables.FileSystemDataSource(directory);

        var manifest = await source.GetManifestAsync(CancellationToken.None);

        manifest.Version.Should().Be(s_HashA);
        manifest.Entries.Should().ContainSingle();
        manifest.Entries[0].Name.Should().Be("Declared");
        manifest.Entries[0].Length.Should().Be(42);
        manifest.Entries[0].SourceName.Should().Be(directory);
    }

    [Fact]
    public async Task FileSystemDataSource_ShouldFallbackToStableDirectoryEnumerationWithoutManifest()
    {
        var directory = CreateDirectory("dt_manifest_fallback_");
        Directory.CreateDirectory(Path.Combine(directory, "nested"));
        await File.WriteAllBytesAsync(Path.Combine(directory, "Z.bytes"), [1]);
        await File.WriteAllBytesAsync(Path.Combine(directory, "nested", "A.bytes"), [1, 2]);

        var manifest = await new global::DataTables.FileSystemDataSource(directory).GetManifestAsync(CancellationToken.None);

        manifest.Version.Should().BeNull();
        manifest.Entries.Select(entry => (entry.Name, entry.Length)).Should().Equal(("Z", 1L), ("nested/A", 2L));
    }

    public static IEnumerable<object[]> InvalidManifests()
    {
        yield return [CreateManifestJson(2, s_HashA, new ManifestEntry("Item", 1, s_HashB, s_HashB))];
        yield return [CreateManifestJson(version: s_HashA.ToUpperInvariant(), new ManifestEntry("Item", 1, s_HashB, s_HashB))];
        yield return [CreateManifestJson(version: s_HashA, new ManifestEntry(string.Empty, 1, s_HashB, s_HashB))];
        yield return [CreateManifestJson(version: s_HashA, new ManifestEntry("Item", -1, s_HashB, s_HashB))];
        yield return [CreateManifestJson(version: s_HashA, new ManifestEntry("Item", 1, s_HashB.ToUpperInvariant(), s_HashB.ToUpperInvariant()))];
        yield return [CreateManifestJson(version: s_HashA, new ManifestEntry("Item", 1, s_HashA, s_HashB))];
        yield return [CreateManifestJson(version: s_HashA,
            new ManifestEntry("Item", 1, s_HashB, s_HashB),
            new ManifestEntry("Item", 1, s_HashB, s_HashB))];
    }

    [Theory]
    [MemberData(nameof(InvalidManifests))]
    public async Task RuntimeManifestParser_ShouldRejectMalformedContractsWithStructuredErrors(string json)
    {
        var directory = CreateDirectory("dt_manifest_invalid_");
        await File.WriteAllTextAsync(Path.Combine(directory, "manifest.json"), json);
        var source = new global::DataTables.FileSystemDataSource(directory);

        var action = async () => await source.GetManifestAsync(CancellationToken.None);

        var exception = await action.Should().ThrowAsync<global::DataTables.DataSourceException>();
        exception.Which.SourceType.Should().Be(global::DataTables.DataSourceType.FileSystem);
        exception.Which.Operation.Should().Be(global::DataTables.DataSourceOperation.GetManifest);
        exception.Which.LogicalName.Should().Be("manifest.json");
        exception.Which.InnerException.Should().BeOfType<InvalidDataException>();
    }

    [Fact]
    public async Task NetworkManifest_ShouldGetEveryTimeAndExposeStructuredHttpAndJsonErrors()
    {
        var requests = new List<(HttpMethod Method, string Uri)>();
        var validJson = CreateManifestJson(version: s_HashA, new ManifestEntry("Item", 3, s_HashB, s_HashB));
        var handler = new DelegateHandler((request, _) =>
        {
            requests.Add((request.Method, request.RequestUri!.ToString()));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(validJson, Encoding.UTF8, "application/json")
            });
        });
        var source = new global::DataTables.NetworkDataSource(
            "https://cdn.example.com/data/",
            "meta/manifest.json",
            httpClient: new HttpClient(handler));

        var first = await source.GetManifestAsync(CancellationToken.None);
        var second = await source.GetManifestAsync(CancellationToken.None);

        first.Entries.Single().SourceName.Should().Be("https://cdn.example.com/data");
        second.Version.Should().Be(s_HashA);
        requests.Should().Equal(
            (HttpMethod.Get, "https://cdn.example.com/data/meta/manifest.json"),
            (HttpMethod.Get, "https://cdn.example.com/data/meta/manifest.json"));

        var httpFailure = new global::DataTables.NetworkDataSource(
            "https://cdn.example.com/data",
            "manifest.json",
            httpClient: new HttpClient(new DelegateHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)))));
        var httpAction = async () => await httpFailure.GetManifestAsync(CancellationToken.None);
        var httpException = await httpAction.Should().ThrowAsync<global::DataTables.DataSourceException>();
        httpException.Which.HttpStatusCode.Should().Be(503);
        httpException.Which.Location.Should().Be("https://cdn.example.com/data/manifest.json");

        var jsonFailure = new global::DataTables.NetworkDataSource(
            "https://cdn.example.com/data",
            "manifest.json",
            httpClient: new HttpClient(new DelegateHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("not-json")
            }))));
        var jsonAction = async () => await jsonFailure.GetManifestAsync(CancellationToken.None);
        var jsonException = await jsonAction.Should().ThrowAsync<global::DataTables.DataSourceException>();
        jsonException.Which.InnerException.Should().BeOfType<InvalidDataException>();
    }

    [Fact]
    public async Task NetworkManifestCancellation_ShouldRemainOperationCanceledException()
    {
        var handler = new DelegateHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("unreachable");
        });
        var source = new global::DataTables.NetworkDataSource(
            "https://cdn.example.com/data",
            "manifest.json",
            httpClient: new HttpClient(handler));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var action = async () => await source.GetManifestAsync(cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task NetworkAvailability_ShouldReturnFalseOnlyForExplicitNotFoundAndDiagnoseOtherFailures()
    {
        var missing = new global::DataTables.NetworkDataSource(
            "https://cdn.example.com/data",
            "manifest.json",
            httpClient: new HttpClient(new DelegateHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)))));
        (await missing.IsAvailableAsync(CancellationToken.None)).Should().BeFalse();

        var unavailable = new global::DataTables.NetworkDataSource(
            "https://cdn.example.com/data",
            "manifest.json",
            httpClient: new HttpClient(new DelegateHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)))));
        var action = async () => await unavailable.IsAvailableAsync(CancellationToken.None);

        var exception = await action.Should().ThrowAsync<global::DataTables.DataSourceException>();
        exception.Which.Operation.Should().Be(global::DataTables.DataSourceOperation.IsAvailable);
        exception.Which.HttpStatusCode.Should().Be(503);
    }

    [Fact]
    public async Task NetworkDataSource_ShouldNotAutomaticallyValidateDownloadedPayloadAgainstManifestHash()
    {
        var payload = new byte[] { 1, 2, 3 };
        var manifestJson = CreateManifestJson(version: s_HashA, new ManifestEntry("Folder/My Table", payload.Length, s_HashB, s_HashB));
        var requests = new List<string>();
        var handler = new DelegateHandler((request, _) =>
        {
            requests.Add(request.RequestUri!.OriginalString);
            var content = request.RequestUri.AbsolutePath.EndsWith("manifest.json", StringComparison.Ordinal)
                ? new StringContent(manifestJson, Encoding.UTF8, "application/json")
                : new ByteArrayContent(payload);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        });
        var source = new global::DataTables.NetworkDataSource(
            "https://cdn.example.com/data",
            "manifest.json",
            httpClient: new HttpClient(handler));

        _ = await source.GetManifestAsync(CancellationToken.None);
        var downloaded = await source.LoadAsync("Folder/My Table", CancellationToken.None);

        downloaded.Should().Equal(payload, "NetworkDataSource leaves decoded-payload validation to HashValidatedDataSource");
        requests.Should().Contain("https://cdn.example.com/data/Folder/My%20Table.bytes");
    }

    [Fact]
    public async Task StreamingAssetsDesktopPath_ShouldUseConfiguredManifestAndMapFailuresToStreamingAssets()
    {
        var directory = CreateDirectory("dt_streaming_assets_");
        await File.WriteAllTextAsync(Path.Combine(directory, "runtime.json"), CreateManifestJson(
            version: s_HashA,
            new ManifestEntry("Item", 1, s_HashB, s_HashB)));
        var source = new global::DataTables.StreamingAssetsDataSource(directory, "runtime.json");

        var manifest = await source.GetManifestAsync(CancellationToken.None);

        manifest.Entries.Should().ContainSingle(entry => entry.Name == "Item");

        await File.WriteAllTextAsync(Path.Combine(directory, "runtime.json"), "invalid");
        var action = async () => await source.GetManifestAsync(CancellationToken.None);
        var exception = await action.Should().ThrowAsync<global::DataTables.DataSourceException>();
        exception.Which.SourceType.Should().Be(global::DataTables.DataSourceType.StreamingAssets);
        exception.Which.Operation.Should().Be(global::DataTables.DataSourceOperation.GetManifest);
        exception.Which.InnerException.Should().BeOfType<global::DataTables.DataSourceException>();
    }

    [Fact]
    public void StreamingAssetsUriBuilder_ShouldEscapeEachRelativeSegmentAndRejectTraversal()
    {
        var source = new global::DataTables.StreamingAssetsDataSource("jar:file:///app/base!/assets");
        var method = typeof(global::DataTables.StreamingAssetsDataSource)
            .GetMethod("GetResourceLocation", BindingFlags.Instance | BindingFlags.NonPublic)!;

        var location = (string)method.Invoke(source, ["folder name/配置.bytes"])!;

        location.Should().Be("jar:file:///app/base!/assets/folder%20name/%E9%85%8D%E7%BD%AE.bytes");
        var action = () => method.Invoke(source, ["../escape.bytes"]);
        action.Should().Throw<TargetInvocationException>().WithInnerException<ArgumentException>();
    }

    private static void AssertManifestMatchesOutputs(ManifestSnapshot manifest, string dataDirectory)
    {
        manifest.FormatVersion.Should().Be(1);
        manifest.Entries.Keys.Should().BeInAscendingOrder(StringComparer.Ordinal);
        foreach (var pair in manifest.Entries)
        {
            pair.Key.Should().NotEndWith(".bytes");
            var output = Path.Combine(dataDirectory, pair.Key + ".bytes");
            var bytes = File.ReadAllBytes(output);
            pair.Value.Length.Should().Be(bytes.LongLength);
            pair.Value.Hash.Should().Be(Sha256(bytes));
            pair.Value.Version.Should().Be(pair.Value.Hash);
            pair.Value.Hash.Should().MatchRegex("^[0-9a-f]{64}$");
        }

        var canonical = string.Concat(manifest.Entries.Select(pair =>
            pair.Key + "\0" + pair.Value.Length.ToString(CultureInfo.InvariantCulture) + "\0" + pair.Value.Hash + "\n"));
        manifest.Version.Should().Be(Sha256(Encoding.UTF8.GetBytes(canonical)));
    }

    private static ManifestSnapshot ReadManifest(byte[] utf8Json)
    {
        using var document = JsonDocument.Parse(utf8Json);
        var root = document.RootElement;
        var entries = root.GetProperty("entries").EnumerateArray().ToDictionary(
            entry => entry.GetProperty("name").GetString()!,
            entry => new ManifestEntry(
                entry.GetProperty("name").GetString()!,
                entry.GetProperty("length").GetInt64(),
                entry.GetProperty("version").GetString()!,
                entry.GetProperty("hash").GetString()!),
            StringComparer.Ordinal);
        return new ManifestSnapshot(
            root.GetProperty("formatVersion").GetInt32(),
            root.GetProperty("version").GetString()!,
            entries);
    }

    private static string CreateManifestJson(string version, params ManifestEntry[] entries)
        => CreateManifestJson(1, version, entries);

    private static string CreateManifestJson(int formatVersion, string version, params ManifestEntry[] entries)
    {
        return JsonSerializer.Serialize(new
        {
            formatVersion,
            version,
            entries = entries.Select(entry => new
            {
                name = entry.Name,
                length = entry.Length,
                version = entry.Version,
                hash = entry.Hash,
            })
        });
    }

    private static Task<GenerationResult> GenerateAsync(TestPaths paths, GenerationMode mode = GenerationMode.CodeAndData)
    {
        return new DataTableGenerator().GenerateFile(
            inputDirectories: [paths.Input],
            searchPatterns: ["*.xlsx"],
            codeOutputDir: paths.Code,
            dataOutputDir: paths.Data,
            usingNamespace: "DataTables.Tests.Manifest",
            dataRowClassPrefix: "DR",
            importNamespaces: string.Empty,
            filterColumnTags: string.Empty,
            forceOverwrite: true,
            logger: _ => { },
            generationMode: mode);
    }

    private static async Task CreateWorkbookAsync(string path, int itemId, int otherId)
    {
        using var workbook = new XSSFWorkbook();
        AddTableSheet(workbook, "Items", "Item", itemId);
        AddTableSheet(workbook, "Others", "Other", otherId);
        await using var stream = File.Create(path);
        workbook.Write(stream);
    }

    private static void AddTableSheet(XSSFWorkbook workbook, string sheetName, string className, int id)
    {
        var sheet = workbook.CreateSheet(sheetName);
        sheet.CreateRow(0).CreateCell(0, CellType.String).SetCellValue($"dtgen=table, class={className}");
        sheet.CreateRow(1).CreateCell(0, CellType.String).SetCellValue("Identifier");
        sheet.CreateRow(2).CreateCell(0, CellType.String).SetCellValue("Id");
        sheet.CreateRow(3).CreateCell(0, CellType.String).SetCellValue("int");
        sheet.CreateRow(4).CreateCell(0, CellType.Numeric).SetCellValue(id);
    }

    private static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static TestPaths CreatePaths()
    {
        var root = CreateDirectory("dt_runtime_manifest_");
        return new TestPaths(
            Directory.CreateDirectory(Path.Combine(root, "input")).FullName,
            Directory.CreateDirectory(Path.Combine(root, "code")).FullName,
            Directory.CreateDirectory(Path.Combine(root, "data")).FullName);
    }

    private static string CreateDirectory(string prefix)
    {
        var path = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class DelegateHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> m_Handler;

        public DelegateHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            m_Handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => m_Handler(request, cancellationToken);
    }

    private sealed record TestPaths(string Input, string Code, string Data);
    private sealed record ManifestEntry(string Name, long Length, string Version, string Hash);
    private sealed record ManifestSnapshot(int FormatVersion, string Version, IReadOnlyDictionary<string, ManifestEntry> Entries);
}
