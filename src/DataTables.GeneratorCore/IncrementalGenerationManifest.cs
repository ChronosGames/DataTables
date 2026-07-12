using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DataTables.GeneratorCore;

internal sealed class IncrementalGenerationManifest
{
    public const int CurrentVersion = 1;
    public const string FileName = ".dtgen-manifest.json";

    public int Version { get; set; } = CurrentVersion;

    public string GeneratorFingerprint { get; set; } = string.Empty;

    public string CodeOutputDirectory { get; set; } = string.Empty;

    public string DataOutputDirectory { get; set; } = string.Empty;

    public Dictionary<string, IncrementalInputEntry> Inputs { get; set; } = new();

    public static IncrementalGenerationManifest Load(string path, Action<string> logger)
    {
        if (!File.Exists(path))
        {
            return new IncrementalGenerationManifest();
        }

        try
        {
            var manifest = JsonSerializer.Deserialize<IncrementalGenerationManifest>(File.ReadAllText(path));
            if (manifest == null || manifest.Version != CurrentVersion || manifest.Inputs == null)
            {
                logger($"Incremental manifest version changed, regenerate all inputs: {path}");
                return new IncrementalGenerationManifest();
            }

            foreach (var entry in manifest.Inputs.Values)
            {
                if (entry == null || entry.Outputs == null || entry.Registrations == null)
                {
                    logger($"Incremental manifest is invalid, regenerate all inputs: {path}");
                    return new IncrementalGenerationManifest();
                }
            }

            manifest.Inputs = new Dictionary<string, IncrementalInputEntry>(manifest.Inputs, PathComparer);
            return manifest;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            logger($"Incremental manifest is unavailable, regenerate all inputs: {exception.Message}");
            return new IncrementalGenerationManifest();
        }
    }

    public string Serialize()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    }

    public static string ComputeFileHash(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    public static string ComputeGeneratorFingerprint(string usingNamespace, string dataRowClassPrefix, string importNamespaces, ParseOptions options, bool generateCode)
    {
        var values = new[]
        {
            $"manifest={CurrentVersion}",
            $"module={typeof(DataTableGenerator).Module.ModuleVersionId:N}",
            $"namespace={usingNamespace}",
            $"rowPrefix={dataRowClassPrefix}",
            $"imports={importNamespaces}",
            $"generateCode={generateCode}",
            $"strictNames={options.StrictNameValidation}",
            $"validateFormulas={options.ValidateFormulaConsistency}",
            $"formulaPolicy={options.FormulaPolicy}",
            $"columnComment={options.ColumnCommentMarkerText}",
            $"rowComment={options.RowCommentMarkerText}",
            $"skipCell={options.SkipCellMarker}",
            $"filterTags={options.FilterColumnTags}",
            $"arraySeparators={options.ArrayNestedSeparators}",
            $"culture={options.Culture.Name}",
        };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', values))));
    }

    public static string GetInputId(string path)
    {
        return Path.GetFullPath(path).Replace(Path.DirectorySeparatorChar, '/');
    }

    public static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}

internal sealed class IncrementalInputEntry
{
    public string ContentHash { get; set; } = string.Empty;

    public List<IncrementalOutput> Outputs { get; set; } = [];

    public List<IncrementalRegistration> Registrations { get; set; } = [];
}

internal sealed class IncrementalOutput
{
    public string Kind { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string ContentHash { get; set; } = string.Empty;
}

internal sealed class IncrementalRegistration
{
    public string TableFullName { get; set; } = string.Empty;

    public string Child { get; set; } = string.Empty;

    public string Priority { get; set; } = "Normal";
}
