using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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

    public Task<GenerationResult> GenerateFile(string[] inputDirectories, string[] searchPatterns, string codeOutputDir, string dataOutputDir, string usingNamespace, string dataRowClassPrefix, string importNamespaces, string filterColumnTags, bool forceOverwrite, Action<string> logger, ParseOptions? options = null, string? diagnosticsJsonOutput = null)
    {
        var generationMode = string.IsNullOrWhiteSpace(codeOutputDir)
            ? GenerationMode.DataOnly
            : GenerationMode.CodeAndData;
        return GenerateFile(inputDirectories, searchPatterns, codeOutputDir, dataOutputDir, usingNamespace, dataRowClassPrefix, importNamespaces, filterColumnTags, forceOverwrite, logger, generationMode, options, diagnosticsJsonOutput);
    }

    public async Task<GenerationResult> GenerateFile(string[] inputDirectories, string[] searchPatterns, string codeOutputDir, string dataOutputDir, string usingNamespace, string dataRowClassPrefix, string importNamespaces, string filterColumnTags, bool forceOverwrite, Action<string> logger, GenerationMode generationMode, ParseOptions? options = null, string? diagnosticsJsonOutput = null)
    {
        // By default, ExcelDataReader throws a NotSupportedException "No data is available for encoding 1252." on .NET Core.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        dataRowClassPrefix ??= string.Empty;
        if (generationMode is not GenerationMode.CodeAndData and not GenerationMode.DataOnly and not GenerationMode.ValidateOnly)
        {
            throw new ArgumentOutOfRangeException(nameof(generationMode), generationMode, "Unknown generation mode.");
        }

        var generateCode = generationMode == GenerationMode.CodeAndData;
        var validateOnly = generationMode == GenerationMode.ValidateOnly;
        var renderCode = generateCode || validateOnly;
        if (generateCode && string.IsNullOrWhiteSpace(codeOutputDir))
        {
            throw new ArgumentException("Code output directory is required in code-and-data generation mode.", nameof(codeOutputDir));
        }
        if (!generateCode)
        {
            codeOutputDir = string.Empty;
        }

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
        var seenInputPaths = new HashSet<string>(IncrementalGenerationManifest.PathComparer);
        for (var rootIndex = 0; rootIndex < inputDirectories.Length; rootIndex++)
        {
            var dir = Path.GetFullPath(inputDirectories[rootIndex]);
            foreach (var searchPattern in searchPatterns)
            {
                foreach (var filePath in Directory.EnumerateFiles(dir, searchPattern, SearchOption.AllDirectories).OrderBy(path => path, IncrementalGenerationManifest.PathComparer))
                {
                    var fileName = Path.GetFileName(filePath);
                    if (fileName.StartsWith('~'))
                    {
                        continue;
                    }

                    // Only process formats backed by the current generator pipeline:
                    // .xlsx/.xlsm use NPOI workbooks directly; .csv is mapped to one in-memory worksheet.
                    if (!IsSupportedInputFile(fileName))
                    {
                        continue;
                    }

                    var fullPath = Path.GetFullPath(filePath);
                    if (!seenInputPaths.Add(fullPath))
                    {
                        logger($"Repeated file: {fullPath}");
                        continue;
                    }

                    var id = IncrementalGenerationManifest.GetInputId(rootIndex, dir, fullPath);
                    filePaths.Add(id, fullPath);
                }
            }
        }

        var codeAndDataManifestPath = validateOnly || string.IsNullOrWhiteSpace(dataOutputDir)
            ? string.Empty
            : Path.Combine(dataOutputDir, IncrementalGenerationManifest.FileName);
        var manifestPath = validateOnly
            ? string.Empty
            : generationMode == GenerationMode.DataOnly
                ? Path.Combine(dataOutputDir, IncrementalGenerationManifest.DataOnlyFileName)
                : codeAndDataManifestPath;
        var previousManifest = validateOnly
            ? new IncrementalGenerationManifest()
            : generationMode == GenerationMode.DataOnly && !File.Exists(manifestPath)
                ? IncrementalGenerationManifest.Load(codeAndDataManifestPath, logger)
                : IncrementalGenerationManifest.Load(manifestPath, logger);
        if (filePaths.Count == 0 && previousManifest.Inputs.Count == 0)
        {
            throw new InvalidOperationException("Not found Excel files, inputDir: " + inputDirectories.Length);
        }

        var generatorFingerprint = IncrementalGenerationManifest.ComputeGeneratorFingerprint(
            usingNamespace,
            dataRowClassPrefix,
            importNamespaces,
            parseOptions,
            renderCode);
        var canReuseManifest = !validateOnly && !forceOverwrite && previousManifest.GeneratorFingerprint == generatorFingerprint;
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
        var stagingDataOutputDir = validateOnly ? string.Empty : transaction.GetStagingDirectory(dataOutputDir);

        var allDiagnostics = new System.Collections.Concurrent.ConcurrentBag<Diagnostic>();
        var allMetrics = new System.Collections.Concurrent.ConcurrentBag<DiagnosticsMetrics>();
        var contextsByInput = new ConcurrentDictionary<string, ConcurrentBag<GenerationContext>>(IncrementalGenerationManifest.PathComparer);
        var outputClaims = new OutputClaimRegistry();
        var hasValidTagFilter = TagFilterUtils.TryValidateExpression(parseOptions.FilterColumnTags, out var tagFilterError);
        if (!hasValidTagFilter)
        {
            allDiagnostics.Add(new Diagnostic(DiagnosticSeverity.Error, string.Empty, string.Empty, string.Empty, tagFilterError));
        }
        if (generateCode)
        {
            outputClaims.ReserveGeneratedManager();
        }
        foreach (var inputId in unchangedInputIds)
        {
            foreach (var output in previousManifest.Inputs[inputId].Outputs)
            {
                if (!outputClaims.TrySeed(output, inputId, out var conflict))
                {
                    failures.Add(new GenerationFailure(inputId, null, new InvalidOperationException(conflict)));
                }
            }
        }

        if (hasValidTagFilter)
        {
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
                options: parseOptions,
                collectDiagnostic: d => allDiagnostics.Add(d),
                collectMetrics: m => allMetrics.Add(m),
                collectContext: context => contextsByInput.GetOrAdd(pair.Key, _ => new ConcurrentBag<GenerationContext>()).Add(context),
                generateCode: generateCode,
                renderCode: renderCode,
                validateOnly: validateOnly,
                outputClaims: outputClaims,
                failures: failures,
                log: logger)));
        }

        var nextManifest = new IncrementalGenerationManifest
        {
            GeneratorFingerprint = generatorFingerprint,
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
            nextManifest.Inputs.Add(pair.Key, CreateInputEntry(contentHashes[pair.Key], contexts ?? [], renderCode));
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
        if (generateCode)
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

        var parserDiagnostics = allDiagnostics
            .OrderBy(x => x.File, StringComparer.Ordinal)
            .ThenBy(x => x.Sheet, StringComparer.Ordinal)
            .ThenBy(x => x.Cell, StringComparer.Ordinal)
            .ThenBy(x => x.Message, StringComparer.Ordinal)
            .ToArray();
        foreach (var diagnostic in parserDiagnostics.Where(x => x.Severity == DiagnosticSeverity.Error))
        {
            var location = string.IsNullOrEmpty(diagnostic.Cell) ? string.Empty : $" ({diagnostic.Cell})";
            failures.Add(new GenerationFailure(
                diagnostic.File,
                diagnostic.Sheet,
                new InvalidDataException($"Generator diagnostic error{location}: {diagnostic.Message}")));
        }

        if (!validateOnly && failures.IsEmpty)
        {
            try
            {
                PopulateOutputHashes(nextManifest, stagingCodeOutputDir, codeOutputDir, stagingDataOutputDir, dataOutputDir);
                File.WriteAllText(
                    transaction.GetStagingFile(Path.Combine(dataOutputDir, RuntimeDataManifest.FileName)),
                    CreateRuntimeManifest(nextManifest, stagingDataOutputDir, dataOutputDir));
                File.WriteAllText(transaction.GetStagingFile(manifestPath), nextManifest.Serialize());
                ScheduleRemovedOutputs(transaction, previousManifest, nextManifest, generationMode, codeOutputDir, dataOutputDir);
                transaction.Commit();
            }
            catch (Exception exception)
            {
                failures.Add(new GenerationFailure(
                    dataOutputDir,
                    null,
                    new InvalidOperationException($"Generation transaction failed for code root '{codeOutputDir}' and data root '{dataOutputDir}': {exception.Message}", exception)));
            }
        }
        transaction.Dispose();

        logger(string.Empty);
        logger("===========================================================");
        var failureList = failures
            .OrderBy(failure => failure.FilePath, StringComparer.Ordinal)
            .ThenBy(failure => failure.SheetName, StringComparer.Ordinal)
            .ThenBy(failure => failure.Exception.Message, StringComparer.Ordinal)
            .ToArray();
        var synthesizedDiagnostics = failureList
            .Where(failure => !failure.Exception.Message.StartsWith("Generator diagnostic error", StringComparison.Ordinal))
            .Select(failure => new Diagnostic(
                DiagnosticSeverity.Error,
                failure.FilePath,
                failure.SheetName ?? string.Empty,
                string.Empty,
                failure.Exception.Message));
        var diagnosticList = parserDiagnostics
            .Concat(synthesizedDiagnostics)
            .OrderBy(x => x.File, StringComparer.Ordinal)
            .ThenBy(x => x.Sheet, StringComparer.Ordinal)
            .ThenBy(x => x.Cell, StringComparer.Ordinal)
            .ThenBy(x => x.Message, StringComparer.Ordinal)
            .ToArray();

        // 诊断报告不属于生成事务，即使提交失败或回滚也必须可用。
        if (!string.IsNullOrEmpty(diagnosticsJsonOutput))
        {
            var report = new DiagnosticsReport
            {
                InfoCount = diagnosticList.Count(x => x.Severity == DiagnosticSeverity.Info),
                WarningCount = diagnosticList.Count(x => x.Severity == DiagnosticSeverity.Warning),
                ErrorCount = diagnosticList.Count(x => x.Severity == DiagnosticSeverity.Error),
                Items = diagnosticList.ToList(),
                Metrics = metricsList
            };
            var json = System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            WriteDiagnosticsReport(diagnosticsJsonOutput, json);
            logger($"Diagnostics report saved: {diagnosticsJsonOutput}");
        }

        var succeededCount = list.Count(x => !x.Skiped);
        var skippedCount = list.Count(x => x.Skiped)
            + unchangedInputIds.Sum(id => previousManifest.Inputs[id].Registrations.Count);
        logger(validateOnly
            ? $"数据表校验完成: {succeededCount} 成功，{failureList.Length} 失败，{skippedCount} 已跳过"
            : $"数据表导出完成: {succeededCount} 成功，{failureList.Length} 失败，{skippedCount} 已跳过");

        return new GenerationResult(succeededCount, skippedCount, failureList, diagnosticList);
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

    private static string CreateRuntimeManifest(IncrementalGenerationManifest manifest, string stagingDataOutputDir, string finalDataOutputDir)
    {
        var inputs = manifest.Inputs.Values
            .SelectMany(entry => entry.Outputs)
            .Where(output => output.Kind == "data")
            .Select(output =>
            {
                var stagingPath = ResolveOutputPath(output, string.Empty, stagingDataOutputDir);
                var finalPath = ResolveOutputPath(output, string.Empty, finalDataOutputDir);
                var path = stagingPath != null && File.Exists(stagingPath) ? stagingPath : finalPath;
                if (path == null || !File.Exists(path)) throw new InvalidOperationException($"Generated data output was not found: {output.Path}");
                var name = Path.ChangeExtension(output.Path, null)!.Replace('\\', '/');
                return new RuntimeDataManifestInput(name, path);
            });
        return RuntimeDataManifest.Create(inputs);
    }

    private static void ScheduleRemovedOutputs(GenerationTransaction transaction, IncrementalGenerationManifest previousManifest, IncrementalGenerationManifest nextManifest, GenerationMode generationMode, string codeOutputDir, string dataOutputDir)
    {
        var nextOutputKeys = nextManifest.Inputs.Values
            .SelectMany(entry => entry.Outputs)
            .Select(GetOutputKey)
            .ToHashSet(IncrementalGenerationManifest.PathComparer);

        foreach (var output in previousManifest.Inputs.Values.SelectMany(entry => entry.Outputs))
        {
            if (generationMode == GenerationMode.DataOnly && output.Kind != "data")
            {
                continue;
            }

            if (nextOutputKeys.Contains(GetOutputKey(output)))
            {
                continue;
            }

            var path = ResolveOutputPath(output, codeOutputDir, dataOutputDir);
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

    private static bool IsSupportedInputFile(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".xlsm", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".csv", StringComparison.OrdinalIgnoreCase);
    }

    private static XSSFWorkbook CreateWorkbook(string filePath, Stream stream)
    {
        if (!Path.GetExtension(filePath).Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return new XSSFWorkbook(stream);
        }

        // CSV inputs represent a single logical worksheet and then reuse the existing workbook parser.
        var workbook = new XSSFWorkbook();
        var sheet = workbook.CreateSheet(GetCsvSheetName(filePath));
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var rowIndex = 0;
        foreach (var fields in ReadCsvRows(reader))
        {
            var row = sheet.CreateRow(rowIndex++);
            for (var columnIndex = 0; columnIndex < fields.Count; columnIndex++)
            {
                row.CreateCell(columnIndex, CellType.String).SetCellValue(fields[columnIndex]);
            }
        }

        return workbook;
    }

    private static string GetCsvSheetName(string filePath)
    {
        var sheetName = Path.GetFileNameWithoutExtension(filePath);
        if (string.IsNullOrWhiteSpace(sheetName))
        {
            return "CSV";
        }

        var invalidChars = new HashSet<char>(new[] { ':', '\\', '/', '?', '*', '[', ']' });
        var sanitized = new string(sheetName.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray()).Trim('\'');
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "CSV";
        }

        return sanitized.Length <= 31 ? sanitized : sanitized.Substring(0, 31);
    }

    private static IEnumerable<IReadOnlyList<string>> ReadCsvRows(TextReader reader)
    {
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        int value;
        while ((value = reader.Read()) != -1)
        {
            var ch = (char)value;
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (reader.Peek() == '"')
                    {
                        reader.Read();
                        field.Append('"');
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(ch);
                }

                continue;
            }

            if (ch == '"' && field.Length == 0)
            {
                inQuotes = true;
            }
            else if (ch == ',')
            {
                row.Add(field.ToString());
                field.Clear();
            }
            else if (ch == '\r' || ch == '\n')
            {
                if (ch == '\r' && reader.Peek() == '\n')
                {
                    reader.Read();
                }

                row.Add(field.ToString());
                field.Clear();
                yield return row;
                row = new List<string>();
            }
            else
            {
                field.Append(ch);
            }
        }

        if (inQuotes)
        {
            throw new FormatException("CSV contains an unterminated quoted field.");
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            yield return row;
        }
    }

    private async Task GenerateExcel(string filePath, string usingNamespace, string prefixClassName, string[] usingStrings, string codeOutputDir, string finalCodeOutputDir, string dataOutputDir, string finalDataOutputDir, bool forceOverwrite, ConcurrentBag<GenerationContext> list, Action<string> log, ParseOptions options, Action<Diagnostic> collectDiagnostic, Action<DiagnosticsMetrics> collectMetrics, Action<GenerationContext> collectContext, bool generateCode, bool renderCode, bool validateOnly, OutputClaimRegistry outputClaims, ConcurrentBag<GenerationFailure> failures)
    {
        using (var logger = new ILogger(log))
        {
            await using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                XSSFWorkbook xssWorkbook;
                try
                {
                    xssWorkbook = CreateWorkbook(filePath, stream);
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
                        if (context.Failed)
                        {
                            continue;
                        }

                        if (context.Skiped)
                        {
                            list.Add(context);
                            logger.Debug("  > (skip by table tags).");
                            continue;
                        }

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

                        var codeContent = renderCode ? RenderCodeFile(context) : null;
                        var codeContentHash = codeContent == null ? null : ComputeContentHash(codeContent);
                        if (!outputClaims.TryReserve(context, filePath, codeContentHash, out var writeCode, out var conflict))
                        {
                            var exception = new InvalidOperationException(conflict);
                            context.Failed = true;
                            failures.Add(new GenerationFailure(filePath, context.SheetName, exception));
                            logger.Error("  > {0}.", exception);
                            continue;
                        }

                        // 生成代码文件。分表生成的代码内容相同时只写入一次。
                        if (generateCode && writeCode)
                        {
                            GenerateCodeFile(context, codeOutputDir, finalCodeOutputDir, codeContent!, forceOverwrite, logger);
                        }

                        // 生成数据文件。ValidateOnly 模式只解析、校验并渲染代码模板，不写任何产物。
                        if (!validateOnly)
                        {
                            processor.GenerateDataFile(dataOutputDir, finalDataOutputDir, forceOverwrite, sheet, logger);
                        }

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

    private static string RenderCodeFile(GenerationContext context)
    {
        if (!s_CodeTemplateRendererRegistry.TryGetRenderer(context.DataSetType, out var renderer))
        {
            throw new InvalidOperationException($"未注册 DTGen={context.DataSetType} 的代码生成模板。");
        }

        return renderer.TransformText(context);
    }

    void GenerateCodeFile(GenerationContext context, string outputDir, string comparisonOutputDir, string content, bool forceOverwrite, ILogger logger)
    {
        logger.Debug(WriteToFile(outputDir, comparisonOutputDir, context.DataRowClassName + ".cs", content, forceOverwrite));
    }

    static string NormalizeNewLines(string content)
    {
        // The T4 generated code may be text with mixed line ending types. (CR + CRLF)
        // We need to normalize the line ending type in each Operating Systems. (e.g. Windows=CRLF, Linux/macOS=LF)
        return content.Replace("\r\n", "\n");
    }

    private static string ComputeContentHash(string content)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(NormalizeNewLines(content))));
    }

    private static void WriteDiagnosticsReport(string outputFile, string content)
    {
        var fullPath = Path.GetFullPath(outputFile);
        var directory = Path.GetDirectoryName(fullPath) ?? throw new InvalidOperationException($"Output file has no directory: {outputFile}");
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporaryPath, content);
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
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

    private sealed class OutputClaimRegistry
    {
        private readonly Dictionary<string, OutputClaim> m_Claims = new(IncrementalGenerationManifest.PathComparer);

        public void ReserveGeneratedManager()
        {
            var output = new IncrementalOutput { Kind = "code", Path = "DataTableManagerExtension.cs" };
            m_Claims.Add(GetOutputKey(output), new OutputClaim("generated manager", string.Empty, AllowEquivalentContent: false));
        }

        public bool TrySeed(IncrementalOutput output, string inputId, out string conflict)
        {
            lock (m_Claims)
            {
                var key = GetOutputKey(output);
                var owner = $"incremental input '{inputId}'";
                if (!m_Claims.TryGetValue(key, out var existing))
                {
                    m_Claims.Add(key, new OutputClaim(owner, output.ContentHash, output.Kind == "code"));
                    conflict = string.Empty;
                    return true;
                }

                if (output.Kind == "code"
                    && existing.AllowEquivalentContent
                    && existing.ContentHash == output.ContentHash)
                {
                    conflict = string.Empty;
                    return true;
                }

                conflict = CreateConflictMessage(output.Kind, output.Path, owner, existing.Owner);
                return false;
            }
        }

        public bool TryReserve(GenerationContext context, string filePath, string? codeContentHash, out bool writeCode, out string conflict)
        {
            var owner = $"'{filePath}' ({context.SheetName})";
            var dataOutput = new IncrementalOutput { Kind = "data", Path = context.GetDataOutputFilePath() };
            var dataKey = GetOutputKey(dataOutput);
            var codeOutput = codeContentHash == null
                ? null
                : new IncrementalOutput { Kind = "code", Path = context.DataRowClassName + ".cs" };
            var codeKey = codeOutput == null ? null : GetOutputKey(codeOutput);

            lock (m_Claims)
            {
                if (m_Claims.TryGetValue(dataKey, out var existingData))
                {
                    writeCode = false;
                    conflict = CreateConflictMessage(dataOutput.Kind, dataOutput.Path, owner, existingData.Owner);
                    return false;
                }

                writeCode = codeOutput != null;
                if (codeOutput != null && m_Claims.TryGetValue(codeKey!, out var existingCode))
                {
                    if (!existingCode.AllowEquivalentContent || existingCode.ContentHash != codeContentHash)
                    {
                        writeCode = false;
                        conflict = CreateConflictMessage(codeOutput.Kind, codeOutput.Path, owner, existingCode.Owner);
                        return false;
                    }

                    writeCode = false;
                }

                m_Claims.Add(dataKey, new OutputClaim(owner, string.Empty, AllowEquivalentContent: false));
                if (codeOutput != null && writeCode)
                {
                    m_Claims.Add(codeKey!, new OutputClaim(owner, codeContentHash!, AllowEquivalentContent: true));
                }

                conflict = string.Empty;
                return true;
            }
        }

        private static string CreateConflictMessage(string kind, string path, string owner, string existingOwner)
        {
            return $"Generated {kind} output conflict for '{path}': {owner} conflicts with {existingOwner}.";
        }

        private sealed record OutputClaim(string Owner, string ContentHash, bool AllowEquivalentContent);
    }
}
