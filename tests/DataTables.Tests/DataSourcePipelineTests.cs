using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataTables;
using FluentAssertions;
using Xunit;

namespace DataTables.Tests;

public class DataSourcePipelineTests
{
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

        public ValueTask<byte[]> LoadAsync(string name, CancellationToken cancellationToken)
        {
            if (_throwOnLoad)
            {
                throw new InvalidOperationException($"{_name} load failed");
            }

            return new ValueTask<byte[]>(new byte[] { 1, 2, 3 });
        }

        public ValueTask<bool> ExistsAsync(string name, CancellationToken cancellationToken) => new ValueTask<bool>(_exists);

        public ValueTask<DataSourceManifest> GetManifestAsync(CancellationToken cancellationToken) => new ValueTask<DataSourceManifest>(_manifest);

        public ValueTask<bool> IsAvailableAsync() => new ValueTask<bool>(true);

        public override string ToString() => _name;
    }
}
