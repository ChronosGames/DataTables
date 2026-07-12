using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DataTables.GeneratorCore;

internal sealed class GenerationTransaction : IDisposable
{
    private readonly string m_Id = Guid.NewGuid().ToString("N");
    private readonly Dictionary<string, OutputRoot> m_Roots = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> m_FilesToDelete = new(StringComparer.OrdinalIgnoreCase);
    private bool m_Committed;

    public string GetStagingDirectory(string outputDirectory)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            return string.Empty;
        }

        var finalDirectory = Path.GetFullPath(outputDirectory);
        if (m_Roots.TryGetValue(finalDirectory, out var existing))
        {
            return existing.StagingDirectory;
        }

        Directory.CreateDirectory(finalDirectory);
        var stagingDirectory = Path.Combine(finalDirectory, $".dtgen-{m_Id}");
        Directory.CreateDirectory(stagingDirectory);
        m_Roots.Add(finalDirectory, new OutputRoot(finalDirectory, stagingDirectory));
        return stagingDirectory;
    }

    public string GetStagingFile(string outputFile)
    {
        var fullPath = Path.GetFullPath(outputFile);
        var directory = Path.GetDirectoryName(fullPath) ?? throw new InvalidOperationException($"Output file has no directory: {outputFile}");
        return Path.Combine(GetStagingDirectory(directory), Path.GetFileName(fullPath));
    }

    public void DeleteFile(string outputFile)
    {
        var fullPath = Path.GetFullPath(outputFile);
        if (!m_Roots.Values.Any(root => IsWithinRoot(root.FinalDirectory, fullPath)))
        {
            throw new InvalidOperationException($"Delete target is outside generation output directories: {outputFile}");
        }

        m_FilesToDelete.Add(fullPath);
    }

    public void Commit()
    {
        if (m_Committed)
        {
            throw new InvalidOperationException("Generation transaction has already been committed.");
        }

        var items = m_Roots.Values
            .SelectMany(root => Directory.EnumerateFiles(root.StagingDirectory, "*", SearchOption.AllDirectories)
                .Select(stagedFile => new CommitItem(
                    stagedFile,
                    Path.Combine(root.FinalDirectory, Path.GetRelativePath(root.StagingDirectory, stagedFile)),
                    Path.Combine(root.StagingDirectory, ".backup", Path.GetRelativePath(root.StagingDirectory, stagedFile)))))
            .OrderBy(item => item.FinalFile, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var committed = new List<CommitItem>(items.Length);
        var deleted = new List<DeleteItem>();

        try
        {
            foreach (var item in items)
            {
                var finalDirectory = Path.GetDirectoryName(item.FinalFile)!;
                Directory.CreateDirectory(finalDirectory);

                if (File.Exists(item.FinalFile))
                {
                    if (FilesEqual(item.StagedFile, item.FinalFile))
                    {
                        File.Delete(item.StagedFile);
                        continue;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(item.BackupFile)!);
                    File.Replace(item.StagedFile, item.FinalFile, item.BackupFile, ignoreMetadataErrors: true);
                    committed.Add(item with { ReplacedExisting = true });
                }
                else
                {
                    File.Move(item.StagedFile, item.FinalFile);
                    committed.Add(item);
                }
            }

            foreach (var finalFile in m_FilesToDelete.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                if (!File.Exists(finalFile))
                {
                    continue;
                }

                var root = m_Roots.Values
                    .Where(candidate => IsWithinRoot(candidate.FinalDirectory, finalFile))
                    .OrderByDescending(candidate => candidate.FinalDirectory.Length)
                    .First();
                var backupFile = Path.Combine(root.StagingDirectory, ".deleted", Path.GetRelativePath(root.FinalDirectory, finalFile));
                Directory.CreateDirectory(Path.GetDirectoryName(backupFile)!);
                File.Move(finalFile, backupFile);
                deleted.Add(new DeleteItem(finalFile, backupFile));
            }

            m_Committed = true;
        }
        catch
        {
            RestoreDeletedFiles(deleted);
            RollBack(committed);
            throw;
        }
        finally
        {
            CleanupStagingDirectories();
        }
    }

    public void Dispose()
    {
        CleanupStagingDirectories();
    }

    private static bool FilesEqual(string left, string right)
    {
        var leftInfo = new FileInfo(left);
        var rightInfo = new FileInfo(right);
        return leftInfo.Length == rightInfo.Length && File.ReadAllBytes(left).AsSpan().SequenceEqual(File.ReadAllBytes(right));
    }

    private static void RollBack(List<CommitItem> committed)
    {
        for (var index = committed.Count - 1; index >= 0; index--)
        {
            var item = committed[index];
            if (item.ReplacedExisting)
            {
                if (File.Exists(item.BackupFile))
                {
                    File.Replace(item.BackupFile, item.FinalFile, null, ignoreMetadataErrors: true);
                }
            }
            else if (File.Exists(item.FinalFile))
            {
                File.Delete(item.FinalFile);
            }
        }
    }

    private static void RestoreDeletedFiles(List<DeleteItem> deleted)
    {
        for (var index = deleted.Count - 1; index >= 0; index--)
        {
            var item = deleted[index];
            Directory.CreateDirectory(Path.GetDirectoryName(item.FinalFile)!);
            File.Move(item.BackupFile, item.FinalFile, overwrite: true);
        }
    }

    private static bool IsWithinRoot(string root, string path)
    {
        var relativePath = Path.GetRelativePath(root, path);
        return !Path.IsPathRooted(relativePath)
            && relativePath != ".."
            && !relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    private void CleanupStagingDirectories()
    {
        foreach (var root in m_Roots.Values)
        {
            try
            {
                if (Directory.Exists(root.StagingDirectory))
                {
                    Directory.Delete(root.StagingDirectory, recursive: true);
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private sealed record OutputRoot(string FinalDirectory, string StagingDirectory);

    private sealed record CommitItem(string StagedFile, string FinalFile, string BackupFile, bool ReplacedExisting = false);

    private sealed record DeleteItem(string FinalFile, string BackupFile);
}
