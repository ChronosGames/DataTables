using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DataTables.GeneratorCore;

internal static class RuntimeDataManifest
{
    public const string FileName = "manifest.json";
    public const int FormatVersion = 1;

    public static string Create(IEnumerable<RuntimeDataManifestInput> inputs)
    {
        var entries = inputs
            .Select(input => CreateEntry(input.Name, input.Path))
            .OrderBy(entry => entry.Name, StringComparer.Ordinal)
            .ToArray();
        var duplicate = entries.GroupBy(entry => entry.Name, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
        if (duplicate != null) throw new InvalidOperationException($"Duplicate runtime manifest entry: {duplicate.Key}");

        var canonical = new StringBuilder();
        foreach (var entry in entries)
        {
            canonical.Append(entry.Name);
            canonical.Append('\0');
            canonical.Append(entry.Length.ToString(CultureInfo.InvariantCulture));
            canonical.Append('\0');
            canonical.Append(entry.Hash);
            canonical.Append('\n');
        }

        var document = new RuntimeManifestDocument
        {
            FormatVersion = FormatVersion,
            Version = Sha256Hex(Encoding.UTF8.GetBytes(canonical.ToString())),
            Entries = entries,
        };
        return JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
    }

    private static RuntimeManifestEntry CreateEntry(string name, string path)
    {
        var bytes = File.ReadAllBytes(path);
        var hash = Sha256Hex(bytes);
        return new RuntimeManifestEntry
        {
            Name = name,
            Length = bytes.LongLength,
            Version = hash,
            Hash = hash,
        };
    }

    private static string Sha256Hex(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private sealed class RuntimeManifestDocument
    {
        [JsonPropertyName("formatVersion")]
        public int FormatVersion { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("entries")]
        public RuntimeManifestEntry[] Entries { get; set; } = [];
    }

    private sealed class RuntimeManifestEntry
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("length")]
        public long Length { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("hash")]
        public string Hash { get; set; } = string.Empty;
    }
}

internal sealed record RuntimeDataManifestInput(string Name, string Path);
