using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
#if UNITY_5_3_OR_NEWER
using UnityEngine;
#else
using System.Text.Json;
#endif

namespace DataTables
{
    internal static class DataSourceManifestJson
    {
        public const string FileName = "manifest.json";
        public const int FormatVersion = 1;

        public static DataSourceManifest Parse(byte[] utf8Json, string sourceName)
        {
            if (utf8Json == null) throw new ArgumentNullException(nameof(utf8Json));
            RuntimeManifestDocument? document;
            try
            {
                var json = Encoding.UTF8.GetString(utf8Json);
#if UNITY_5_3_OR_NEWER
                document = JsonUtility.FromJson<RuntimeManifestDocument>(json);
#else
                document = JsonSerializer.Deserialize<RuntimeManifestDocument>(json, new JsonSerializerOptions { IncludeFields = true });
#endif
            }
            catch (Exception exception)
            {
                throw new InvalidDataException($"Runtime manifest JSON is invalid for '{sourceName}'.", exception);
            }

            if (document == null) throw new InvalidDataException($"Runtime manifest is empty for '{sourceName}'.");
            if (document.formatVersion != FormatVersion)
            {
                throw new InvalidDataException($"Unsupported runtime manifest format {document.formatVersion} for '{sourceName}'. Expected {FormatVersion}.");
            }
            if (!IsLowerSha256(document.version)) throw new InvalidDataException($"Runtime manifest version is not a lowercase SHA-256 for '{sourceName}'.");
            if (document.entries == null) throw new InvalidDataException($"Runtime manifest entries are missing for '{sourceName}'.");

            var names = new HashSet<string>(StringComparer.Ordinal);
            var entries = new DataSourceManifestEntry[document.entries.Length];
            for (var i = 0; i < document.entries.Length; i++)
            {
                var item = document.entries[i] ?? throw new InvalidDataException($"Runtime manifest entry {i} is null for '{sourceName}'.");
                if (string.IsNullOrWhiteSpace(item.name)) throw new InvalidDataException($"Runtime manifest entry {i} has an empty name for '{sourceName}'.");
                if (!names.Add(item.name)) throw new InvalidDataException($"Runtime manifest contains duplicate entry '{item.name}' for '{sourceName}'.");
                if (item.length < 0) throw new InvalidDataException($"Runtime manifest entry '{item.name}' has a negative length for '{sourceName}'.");
                if (!IsLowerSha256(item.hash)) throw new InvalidDataException($"Runtime manifest entry '{item.name}' has an invalid hash for '{sourceName}'.");
                if (!IsLowerSha256(item.version)) throw new InvalidDataException($"Runtime manifest entry '{item.name}' has an invalid version for '{sourceName}'.");
                if (!string.Equals(item.version, item.hash, StringComparison.Ordinal)) throw new InvalidDataException($"Runtime manifest entry '{item.name}' version does not match its content hash for '{sourceName}'.");
                entries[i] = new DataSourceManifestEntry(item.name, item.length, item.version, item.hash, sourceName);
            }

            return new DataSourceManifest(entries, document.version);
        }

        private static bool IsLowerSha256(string? value)
        {
            if (value == null || value.Length != 64) return false;
            foreach (var character in value)
            {
                if (!((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f'))) return false;
            }
            return true;
        }

        [Serializable]
        private sealed class RuntimeManifestDocument
        {
            public int formatVersion = default;
            public string? version = default;
            public RuntimeManifestEntry[]? entries = default;
        }

        [Serializable]
        private sealed class RuntimeManifestEntry
        {
            public string? name = default;
            public long length = default;
            public string? version = default;
            public string? hash = default;
        }
    }
}
