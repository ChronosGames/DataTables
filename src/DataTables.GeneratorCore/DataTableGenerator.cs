using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace DataTables.GeneratorCore;

public sealed class DataTableGenerator
{
    private static readonly CodeTemplateRendererRegistry s_CodeTemplateRendererRegistry = CodeTemplateRendererRegistry.CreateDefault();

    private readonly ConcurrentDictionary<string, object> m_Locks;

    public DataTableGenerator()
    {
        m_Locks = new();
    }

    public async Task<GenerationResult> GenerateFile(string[] inputDirectories, string[] searchPatterns, string codeOutputDir, string dataOutputDir, string usingNamespace, string dataRowClassPrefix, string importNamespaces, string filterColumnTags, bool forceOverwrite, Action<string> logger, ParseOptions? options = null, string? diagnosticsJsonOutput = null)
    {
        // By default, ExcelDataReader throws a NotSupportedException "No data is available for encoding 1252." on .NET Core.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        dataRowClassPrefix ??= string.Empty;
        var list = new ConcurrentBag<GenerationContext>();
        var failures = new ConcurrentBag<GenerationFailure>();
        var parseOptions = options ?? new ParseOptions { FilterColumnTags = filterColumnTags };

        // 拼接Import的命名空间
        string[] usingStrings = string.IsNullOrEmpty(importNamespaces) ? Array.Empty<string>() : importNamespaces.Split('&');
        if (usingStrings.Length == 0)
        {
        }
        else if (usingStrings.Length == 1 && string.IsNullOrEmpty(usingStrings[0]))
        {
            usingStrings = Array.Empty<string>();
        }
        else
        {
            usingStrings = usingStrings.Select(x => "using " + x + ';').ToArray();
        }

        // Collect
        var filePaths = new Dictionary<string, string>(IncrementalGenerationManifest.PathComparer);
        foreach (var dir in inputDirectories)
        {
            foreach (var searchPattern in searchPatterns)
            {
                foreach (var filePath in Directory.EnumerateFiles(dir, searchPattern, SearchOption.AllDirectories))
                {
                    var fileName = Path.GetFileName(filePath);
                    if (fileName.StartsWith('~'))
                    {
                        continue;
                    }

                    // 支持的 Excel/CSV 扩展名：
                    //   .xlsx — OOXML（Excel 2007+）
                    //   .xlsm — OOXML 启用宏（结构与 xlsx 相同，仅多出 vbaProject.bin，NPOI XSSFWorkbook 完全兼容）
                    //   .xlsb — Excel 二进制工作簿
                    //   .xls  — 旧版 BIFF
                    //   .csv  — 逗号分隔
                    if (!(fileName.EndsWith(".xlsx", StringComparison.Ordinal) || fileName.EndsWith(".xlsm", StringComparison.Ordinal) || fileName.EndsWith(".xlsb", StringComparison.Ordinal) || fileName.EndsWith(".xls", StringComparison.Ordinal) || fileName.EndsWith(".csv", StringComparison.Ordinal)))
                    {
                        continue;
                    }

                    var id = IncrementalGenerationManifest.GetInputId(filePath);
                    if (filePaths.ContainsKey(id))
                    {
                        logger($"Repeated file: {id}");
                        continue;
                    }

                    filePaths.Add(id, filePath);
                }
            }
        }

        var manifestPath = Path.Combine(dataOutputDir, IncrementalGenerationManifest.FileName);
        var previousManifest = IncrementalGenerationManifest.Load(manifestPath, logger);
        if (filePaths.Count == 0 && previousManifest.Inputs.Count == 0)
        {
            throw new InvalidOperationException("Not found Excel files, inputDir: " + inputDirectories.Length);
        }

        var generatorFingerprint = IncrementalGenerationManifest.ComputeGeneratorFingerprint(
            usingNamespace,
            dataRowClassPrefix,
            importNamespaces,
            parseOptions,
            generateCode: !string.IsNullOrEmpty(codeOutputDir));
        var canReuseManifest = !forceOverwrite && previousManifest.GeneratorFingerprint == generatorFingerprint;
        var contentHashes = new ConcurrentDictionary<string, string>(IncrementalGenerationManifest.PathComparer);
        await Parallel.ForEachAsync(filePaths, (pair, _) =>
        {
            contentHashes[pair.Key] = IncrementalGenerationManifest.ComputeFileHash(pair.Value);
            return ValueTask.CompletedTask;
        });

        var unchangedInputIds = new HashSet<string>(IncrementalGenerationManifest.PathComparer);
        var changedFiles = new Dictionary<string, string>(IncrementalGenerationManifest.PathComparer);
        foreach (var pair in filePaths)
        {
            if (canReuseManifest
                && previousManifest.Inputs.TryGetValue(pair.Key, out var previousEntry)
                && previousEntry.ContentHash == contentHashes[pair.Key]
                && previousEntry.Outputs.All(output => OutputExists(output, codeOutputDir, dataOutputDir)))
            {
                unchangedInputIds.Add(pair.Key);
            }
            else
            {
                changedFiles.Add(pair.Key, pair.Value);
            }
        }

        logger($"Incremental scan: {changedFiles.Count} changed, {unchangedInputIds.Count} unchanged, {previousManifest.Inputs.Keys.Count(x => !filePaths.ContainsKey(x))} removed.");

        using var transaction = new GenerationTransaction();
        var stagingCodeOutputDir = string.IsNullOrEmpty(codeOutputDir) ? string.Empty : transaction.GetStagingDirectory(codeOutputDir);
        var stagingDataOutputDir = transaction.GetStagingDirectory(dataOutputDir);
        if (!string.IsNullOrEmpty(previousManifest.CodeOutputDirectory) && Directory.Exists(previousManifest.CodeOutputDirectory))
        {
            transaction.GetStagingDirectory(previousManifest.CodeOutputDirectory);
        }
        if (!string.IsNullOrEmpty(previousManifest.DataOutputDirectory) && Directory.Exists(previousManifest.DataOutputDirectory))
        {
            transaction.GetStagingDirectory(previousManifest.DataOutputDirectory);
        }

        var allDiagnostics = new System.Collections.Concurrent.ConcurrentBag<Diagnostic>();
        var allMetrics = new System.Collections.Concurrent.ConcurrentBag<DiagnosticsMetrics>();
        var contextsByInput = new ConcurrentDictionary<string, ConcurrentBag<GenerationContext>>(IncrementalGenerationManifest.PathComparer);

        await Parallel.ForEachAsync(changedFiles, (pair, cancellationToken) => new ValueTask(GenerateExcel(pair.Value,
            usingNamespace: usingNamespace,
            forceOverwrite: forceOverwrite,
            dataOutputDir: stagingDataOutputDir,
            finalDataOutputDir: dataOutputDir,
            codeOutputDir: stagingCodeOutputDir,
            finalCodeOutputDir: codeOutputDir,
            list: list,
            prefixClassName: dataRowClassPrefix,
            usingStrings: usingStrings,
            filterColumnTags: filterColumnTags,
            options: parseOptions,
            collectDiagnostic: d => allDiagnostics.Add(d),
            collectMetrics: m => allMetrics.Add(m),
            collectContext: context => contextsByInput.GetOrAdd(pair.Key, _ => new ConcurrentBag<GenerationContext>()).Add(context),
            failures: failures,
            log: logger)));

        var nextManifest = new IncrementalGenerationManifest
        {
            GeneratorFingerprint = generatorFingerprint,
            CodeOutputDirectory = string.IsNullOrEmpty(codeOutputDir) ? string.Empty : Path.GetFullPath(codeOutputDir),
            DataOutputDirectory = Path.GetFullPath(dataOutputDir),
            Inputs = new Dictionary<string, IncrementalInputEntry>(IncrementalGenerationManifest.PathComparer),
        };
        foreach (var pair in filePaths.OrderBy(x => x.Key, IncrementalGenerationManifest.PathComparer))
        {
            if (unchangedInputIds.Contains(pair.Key))
            {
                nextManifest.Inputs.Add(pair.Key, previousManifest.Inputs[pair.Key]);
                continue;
            }

            contextsByInput.TryGetValue(pair.Key, out var contexts);
            nextManifest.Inputs.Add(pair.Key, CreateInputEntry(contentHashes[pair.Key], contexts ?? [], !string.IsNullOrEmpty(codeOutputDir)));
        }

        logger("Generate Manager Files:");

        var registrations = nextManifest.Inputs.Values.SelectMany(x => x.Registrations).ToArray();
        var dict = registrations.GroupBy(k => k.TableFullName, v => v.Child).ToDictionary(k => k.Key, v => v.Where(x => !string.IsNullOrEmpty(x)).OrderBy(x => x));
        var sortedDict = from entry in dict orderby entry.Key ascending select entry;

        // 收集每张表的优先级
        var tablePriorities = registrations
            .GroupBy(x => x.TableFullName)
            .ToDictionary(g => g.Key, g => g.First().Priority);

        // 生成DataTableManagerExtension代码文件(放在未尾确保类名前缀会正确附加)
        if (!string.IsNullOrEmpty(codeOutputDir))
        {
            var dataTableManagerExtensionTemplate = new DataTableManagerExtensionTemplate()
            {
                Namespace = usingNamespace,
                DataTables = sortedDict,
                TablePriorities = tablePriorities,
            };
            logger(WriteToFile(stagingCodeOutputDir, codeOutputDir, "DataTableManagerExtension.cs", dataTableManagerExtensionTemplate.TransformText(), forceOverwrite));
        }

        // 聚合并输出诊断统计（控制台）
        var metricsList = allMetrics.ToList();
        if (metricsList.Count > 0)
        {
            var ignored = metricsList.Sum(m => m.IgnoredFieldCount);
            var tagFiltered = metricsList.Sum(m => m.TagFilteredFieldCount);
            var skippedCols = metricsList.Sum(m => m.SkippedColumnCount);
            var matrixSkipped = metricsList.Sum(m => m.MatrixDefaultSkippedCount);
            var parseMs = metricsList.Sum(m => m.ParseElapsedMs);
            var genMs = metricsList.Sum(m => m.GenerateElapsedMs);
            logger($"Diagnostics Summary: IgnoredFields={ignored}, TagFiltered={tagFiltered}, SkippedColumns={skippedCols}, MatrixDefaultSkipped={matrixSkipped}, ParseMs={parseMs}, GenerateMs={genMs}");

            foreach (var m in metricsList)
            {
                logger($"  - [{m.File}/{m.Sheet}] Ignored={m.IgnoredFieldCount}, TagFiltered={m.TagFilteredFieldCount}, SkippedCols={m.SkippedColumnCount}, MatrixSkipped={m.MatrixDefaultSkippedCount}, ParseMs={m.ParseElapsedMs}, GenMs={m.GenerateElapsedMs}");
            }
        }

        // 输出诊断报告（可选）
        if (!string.IsNullOrEmpty(diagnosticsJsonOutput))
        {
            var report = new DiagnosticsReport
            {
                InfoCount = allDiagnostics.Count(x => x.Severity == DiagnosticSeverity.Info),
                WarningCount = allDiagnostics.Count(x => x.Severity == DiagnosticSeverity.Warning),
                ErrorCount = allDiagnostics.Count(x => x.Severity == DiagnosticSeverity.Error),
                Items = allDiagnostics.ToList(),
                Metrics = metricsList
            };
            var json = System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(transaction.GetStagingFile(diagnosticsJsonOutput), json);
            logger($"Diagnostics report saved: {diagnosticsJsonOutput}");
        }

        PopulateOutputHashes(nextManifest, stagingCodeOutputDir, codeOutputDir, stagingDataOutputDir, dataOutputDir);
        File.WriteAllText(transaction.GetStagingFile(manifestPath), nextManifest.Serialize());
        ScheduleRemovedOutputs(transaction, previousManifest, nextManifest);

        logger(string.Empty);
        logger("===========================================================");
        var failureList = failures.ToArray();
        var succeededCount = list.Count(x => !x.Skiped);
        var skippedCount = list.Count(x => x.Skiped)
            + unchangedInputIds.Sum(id => previousManifest.Inputs[id].Registrations.Count);
        if (failureList.Length == 0)
        {
            transaction.Commit();
        }
        logger($"数据表导出完成: {succeededCount} 成功，{failureList.Length} 失败，{skippedCount} 已跳过");

        return new GenerationResult(succeededCount, skippedCount, failureList);
    }

    private static IncrementalInputEntry CreateInputEntry(string contentHash, IEnumerable<GenerationContext> contexts, bool generateCode)
    {
        var contextList = contexts.ToArray();
        var outputs = contextList
            .SelectMany(context => generateCode
                ? new[]
                {
                    new IncrementalOutput { Kind = "code", Path = context.DataRowClassName + ".cs" },
                    new IncrementalOutput { Kind = "data", Path = context.GetDataOutputFilePath() },
                }
                : new[]
                {
                    new IncrementalOutput { Kind = "data", Path = context.GetDataOutputFilePath() },
                })
            .GroupBy(GetOutputKey, IncrementalGenerationManifest.PathComparer)
            .Select(group => group.First())
            .OrderBy(output => output.Kind, StringComparer.Ordinal)
            .ThenBy(output => output.Path, IncrementalGenerationManifest.PathComparer)
            .ToList();
        var registrations = contextList
            .Select(context => new IncrementalRegistration
            {
                TableFullName = context.DataTableClassFullName,
                Child = context.Child,
                Priority = context.Priority,
            })
            .OrderBy(registration => registration.TableFullName, StringComparer.Ordinal)
            .ThenBy(registration => registration.Child, StringComparer.Ordinal)
            .ToList();

        return new IncrementalInputEntry
        {
            ContentHash = contentHash,
            Outputs = outputs,
            Registrations = registrations,
        };
    }

    private static bool OutputExists(IncrementalOutput output, string codeOutputDir, string dataOutputDir)
    {
        var path = ResolveOutputPath(output, codeOutputDir, dataOutputDir);
        return path != null
            && File.Exists(path)
            && !string.IsNullOrEmpty(output.ContentHash)
            && IncrementalGenerationManifest.ComputeFileHash(path) == output.ContentHash;
    }

    private static void PopulateOutputHashes(IncrementalGenerationManifest manifest, string stagingCodeOutputDir, string finalCodeOutputDir, string stagingDataOutputDir, string finalDataOutputDir)
    {
        foreach (var output in manifest.Inputs.Values.SelectMany(entry => entry.Outputs).Where(output => string.IsNullOrEmpty(output.ContentHash)))
        {
            var stagingPath = ResolveOutputPath(output, stagingCodeOutputDir, stagingDataOutputDir);
            var finalPath = ResolveOutputPath(output, finalCodeOutputDir, finalDataOutputDir);
            var existingPath = stagingPath != null && File.Exists(stagingPath) ? stagingPath : finalPath;
            if (existingPath == null || !File.Exists(existingPath))
            {
                throw new InvalidOperationException($"Generated output was not found: {output.Kind}/{output.Path}");
            }

            output.ContentHash = IncrementalGenerationManifest.ComputeFileHash(existingPath);
        }
    }

    private static void ScheduleRemovedOutputs(GenerationTransaction transaction, IncrementalGenerationManifest previousManifest, IncrementalGenerationManifest nextManifest)
    {
        var nextOutputKeys = nextManifest.Inputs.Values
            .SelectMany(entry => entry.Outputs)
            .Select(GetOutputKey)
            .ToHashSet(IncrementalGenerationManifest.PathComparer);

        foreach (var output in previousManifest.Inputs.Values.SelectMany(entry => entry.Outputs))
        {
            if (nextOutputKeys.Contains(GetOutputKey(output)))
            {
                continue;
            }

            var path = ResolveOutputPath(output, previousManifest.CodeOutputDirectory, previousManifest.DataOutputDirectory);
            if (path != null)
            {
                transaction.DeleteFile(path);
            }
        }
    }

    private static string GetOutputKey(IncrementalOutput output)
    {
        return output.Kind + '\0' + output.Path.Replace('\\', '/');
    }

    private static string? ResolveOutputPath(IncrementalOutput output, string codeOutputDir, string dataOutputDir)
    {
        var root = output.Kind switch
        {
            "code" => codeOutputDir,
            "data" => dataOutputDir,
            _ => string.Empty,
        };
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(output.Path))
        {
            return null;
        }

        var fullRoot = Path.GetFullPath(root);
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, output.Path.Replace('/', Path.DirectorySeparatorChar)));
        var relativePath = Path.GetRelativePath(fullRoot, fullPath);
        if (Path.IsPathRooted(relativePath)
            || relativePath == ".."
            || relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return null;
        }

        return fullPath;
    }

    private async Task GenerateExcel(string filePath, string usingNamespace, string prefixClassName, string[] usingStrings, string filterColumnTags, string codeOutputDir, string finalCodeOutputDir, string dataOutputDir, string finalDataOutputDir, bool forceOverwrite, ConcurrentBag<GenerationContext> list, Action<string> log, ParseOptions options, Action<Diagnostic> collectDiagnostic, Action<DiagnosticsMetrics> collectMetrics, Action<GenerationContext> collectContext, ConcurrentBag<GenerationFailure> failures)
    {
        using (var logger = new ILogger(log))
        {
            await using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                XSSFWorkbook xssWorkbook;
                try
                {
                    xssWorkbook = new XSSFWorkbook(stream);
                }
                catch (Exception e)
                {
                    logger.Error("Generate {0} exception:\n{1}", filePath.Trim('\\'), e);
                    failures.Add(new GenerationFailure(filePath, null, e));
                    return;
                }

                var formulaEvaluator = xssWorkbook.GetCreationHelper().CreateFormulaEvaluator();

                for (int i = 0; i < xssWorkbook.NumberOfSheets; i++)
                {
                    var sheet = xssWorkbook.GetSheetAt(i);
                    if (!ValidSheet(sheet))
                    {
                        continue;
                    }

                    var context = new GenerationContext
                    {
                        FileName = Path.GetFileNameWithoutExtension(filePath),
                        Namespace = usingNamespace,
                        DataRowClassPrefix = prefixClassName,
                        UsingStrings = usingStrings,
                        SheetName = sheet.SheetName.Trim(),
                    };

                    logger.Debug("Generate Excel File: [{0}]({1})", filePath.Trim('\\'), context.SheetName);

                    // 初始化可选解析参数
                    var diagnostics = new DiagnosticsCollector();
                    var processor = new DataTableProcessor(context, formulaEvaluator, options, diagnostics);
                    var genSw = System.Diagnostics.Stopwatch.StartNew();
                    try
                    {
                        // 初始化GenerateContext；若 A1 未声明 DTGen= 则内部静默返回
                        processor.CreateGenerationContext(sheet);
                        if (!processor.ValidateGenerationContext())
                        {
                            if (!string.IsNullOrEmpty(context.DataSetType) && string.IsNullOrEmpty(context.ClassName))
                            {
                                var exception = new InvalidOperationException("数据表缺少 class 配置。");
                                context.Failed = true;
                                failures.Add(new GenerationFailure(filePath, context.SheetName, exception));
                                logger.Error("  > {0}.", exception);
                            }
                            else
                            {
                                logger.Debug("  > (skip).");
                            }
                            continue;
                        }

                        // 生成代码文件
                        if (!string.IsNullOrEmpty(codeOutputDir))
                        {
                            GenerateCodeFile(context, codeOutputDir, finalCodeOutputDir, forceOverwrite, logger);
                        }

                        // 生成数据文件
                        processor.GenerateDataFile(dataOutputDir, finalDataOutputDir, forceOverwrite, sheet, logger);

                        if (context.Failed)
                        {
                            failures.Add(new GenerationFailure(filePath, context.SheetName, context.FailureException ?? new InvalidOperationException("数据表导出失败。")));
                            continue;
                        }

                        // 注册至队列中
                        list.Add(context);
                        collectContext(context);
                    }
                    catch (Exception e)
                    {
                        context.Failed = true;
                        failures.Add(new GenerationFailure(filePath, context.SheetName, e));
                        logger.Error("  > {0}.", e);
                    }
                    finally
                    {
                        genSw.Stop();
                        diagnostics.GetMetrics(context.FileName, context.SheetName).GenerateElapsedMs += genSw.ElapsedMilliseconds;
                        foreach (var d in processor.Diagnostics.Items) collectDiagnostic(d);
                        foreach (var m in processor.Diagnostics.GetAllMetrics()) collectMetrics(m);
                        processor.Dispose();
                    }
                }
            }
        }
    }

    /// <summary>
    /// 检验是否处理该 Sheet。
    /// 规则：
    ///  1. sheet 为 null → 跳过
    ///  2. Sheet 名称以 '#' 开头 → 跳过（注释 Sheet）
    ///
    /// 注意：A1 是否声明 DTGen= 的检查在 DataTableProcessor.CreateGenerationContext 内部完成，
    /// 未声明时会静默返回，不会抛出 FormatException。
    /// </summary>
    private static bool ValidSheet(ISheet? sheet)
    {
        if (sheet == null)
        {
            return false;
        }

        if (sheet.SheetName.TrimStart().StartsWith('#'))
        {
            return false;
        }

        return true;
    }

    void GenerateCodeFile(GenerationContext context, string outputDir, string comparisonOutputDir, bool forceOverwrite, ILogger logger)
    {
        if (!s_CodeTemplateRendererRegistry.TryGetRenderer(context.DataSetType, out var renderer))
        {
            throw new InvalidOperationException($"未注册 DTGen={context.DataSetType} 的代码生成模板。");
        }

        logger.Debug(WriteToFile(outputDir, comparisonOutputDir, context.DataRowClassName + ".cs", renderer.TransformText(context), forceOverwrite));
    }

    static string NormalizeNewLines(string content)
    {
        // The T4 generated code may be text with mixed line ending types. (CR + CRLF)
        // We need to normalize the line ending type in each Operating Systems. (e.g. Windows=CRLF, Linux/macOS=LF)
        return content.Replace("\r\n", "\n");
    }

    string WriteToFile(string directory, string comparisonDirectory, string fileName, string content, bool forceOverwrite)
    {
        var startTickCount = Environment.TickCount;
        var path = Path.Combine(directory, fileName);
        var comparisonPath = Path.Combine(comparisonDirectory, fileName);
        var contentBytes = Encoding.UTF8.GetBytes(NormalizeNewLines(content));

        // If the generated content is unchanged, skip the write.
        if (!forceOverwrite && File.Exists(comparisonPath))
        {
            if (new FileInfo(comparisonPath).Length == contentBytes.Length && contentBytes.AsSpan().SequenceEqual(File.ReadAllBytes(comparisonPath)))
            {
                return $"  > Generate {fileName} to: {comparisonPath} (Skipped) - {Environment.TickCount - startTickCount}ms";
            }
        }

        object lockObj = m_Locks.GetOrAdd(fileName, new object());
        lock (lockObj)
        {
            File.WriteAllBytes(path, contentBytes);
        }
        m_Locks.TryRemove(fileName, out _);

        return $"  > Generate {fileName} to: {comparisonPath} - {Environment.TickCount - startTickCount}ms";
    }
}
