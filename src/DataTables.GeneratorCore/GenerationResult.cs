using System;
using System.Collections.Generic;

namespace DataTables.GeneratorCore;

public sealed class GenerationResult
{
    public GenerationResult(int succeededCount, int skippedCount, IReadOnlyList<GenerationFailure> failures)
        : this(succeededCount, skippedCount, failures, null)
    {
    }

    public GenerationResult(int succeededCount, int skippedCount, IReadOnlyList<GenerationFailure> failures, IReadOnlyList<Diagnostic>? diagnostics)
    {
        SucceededCount = succeededCount;
        SkippedCount = skippedCount;
        Failures = failures;
        Diagnostics = diagnostics ?? Array.Empty<Diagnostic>();
    }

    public int SucceededCount { get; }

    public int SkippedCount { get; }

    public int FailedCount => Failures.Count;

    public bool Succeeded => FailedCount == 0;

    public IReadOnlyList<GenerationFailure> Failures { get; }

    public IReadOnlyList<Diagnostic> Diagnostics { get; }
}

public sealed record GenerationFailure(string FilePath, string? SheetName, Exception Exception)
{
    public override string ToString()
    {
        return string.IsNullOrEmpty(SheetName)
            ? $"{FilePath}: {Exception.Message}"
            : $"{FilePath} ({SheetName}): {Exception.Message}";
    }
}
