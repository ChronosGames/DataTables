using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace DataTables.GeneratorCore;

public sealed class DataTableGenerator
{
    public void GenerateFile(string[] inputDirectories, string[] searchPatterns, string codeOutputDir, string dataOutputDir, string usingNamespace, string prefixClassName, string importNamespaces, string filterColumnTags, bool forceOverwrite, Action<string> logger)
    {
        // By default, ExcelDataReader throws a NotSupportedException "No data is available for encoding 1252." on .NET Core.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        prefixClassName ??= string.Empty;
        var list = new ConcurrentBag<GenerationContext>();

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
        var filePaths = new Dictionary<string, string>();
        foreach (var dir in inputDirectories)
        {
            foreach (var searchPattern in searchPatterns)
            {
                foreach (var filePath in Directory.EnumerateFiles(dir, searchPattern).Where(s => s.EndsWith(".xlsx") || s.EndsWith(".xlsb") || s.EndsWith(".xls") || s.EndsWith(".csv")))
                {
                    var id = filePath.Replace(dir, "");
                    if (filePaths.ContainsKey(id))
                    {
                        logger($"Repeated file: {id}");
                        continue;
                    }

                    filePaths.Add(id, filePath);
                }
            }
        }

        if (filePaths.Count() == 0)
        {
            throw new InvalidOperationException("Not found Excel files, inputDir: " + inputDirectories.Length);
        }

        if (!Directory.Exists(codeOutputDir))
        {
            Directory.CreateDirectory(codeOutputDir);
        }

        if (!Directory.Exists(dataOutputDir))
        {
            Directory.CreateDirectory(dataOutputDir);
        }

        Parallel.ForEach(filePaths, pair => GenerateExcel(pair.Value,
            usingNamespace: usingNamespace,
            forceOverwrite: forceOverwrite,
            dataOutputDir: dataOutputDir,
            codeOutputDir: codeOutputDir,
            list: list,
            prefixClassName: prefixClassName,
            usingStrings: usingStrings,
            filterColumnTags: filterColumnTags,
            log: logger));

        logger("Generate Manager Files:");

        var dict = list.GroupBy(k => k.ClassName, v => v.Child).ToDictionary(k => k.Key, v => v.Where(x => !string.IsNullOrEmpty(x)).OrderBy(x => x));
        var sortedDict = from entry in dict orderby entry.Key ascending select entry;

        // 生成DataTableManagerExtension代码文件(放在未尾确保类名前缀会正确附加)
        var dataTableManagerExtensionTemplate = new DataTableManagerExtensionTemplate()
        {
            Namespace = usingNamespace,
            DataRowPrefix = prefixClassName,
            DataTables = sortedDict,
        };
        logger(WriteToFile(codeOutputDir, "DataTableManagerExtension.cs", dataTableManagerExtensionTemplate.TransformText(), forceOverwrite));

        logger(string.Empty);
        logger("===========================================================");
        logger($"数据表导出完成: {list.Count(x => !x.Failed && !x.Skiped)} 成功，{list.Count(x => x.Failed)} 失败，{list.Count(x => x.Skiped)} 已跳过");
    }

    private static void GenerateExcel(string filePath, string usingNamespace, string prefixClassName, string[] usingStrings, string filterColumnTags, string codeOutputDir, string dataOutputDir, bool forceOverwrite, ConcurrentBag<GenerationContext> list, Action<string> log)
    {
        using (var logger = new ILogger(log))
        {
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                var xssWorkbook = new XSSFWorkbook(stream);
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
                        PrefixClassName = prefixClassName,
                        UsingStrings = usingStrings,
                        SheetName = sheet.SheetName.Trim(),
                    };

                    logger.Debug("Generate Excel File: [{0}]({1})", filePath.Trim('\\'), context.SheetName);

                    using (var processor = new DataTableProcessor(context, filterColumnTags))
                    {
                        // 初始化GenerateContext
                        processor.CreateGenerationContext(sheet);
                        if (!processor.ValidateGenerationContext())
                        {
                            logger.Debug("  > (skip).");
                            continue;
                        }

                        // 生成代码文件
                        GenerateCodeFile(context, codeOutputDir, forceOverwrite, logger);

                        // 生成数据文件
                        processor.GenerateDataFile(filePath, dataOutputDir, forceOverwrite, sheet, logger);

                        // 注册至队列中
                        list.Add(context);
                    }
                }
            }
        }
    }

    // 检验是否过滤该Sheet
    private static bool ValidSheet(ISheet? sheet)
    {
        if (sheet == null)
        {
            return false;
        }

        if (sheet.SheetName.TrimStart().StartsWith("#"))
        {
            return false;
        }

        return true;
    }

    static void GenerateCodeFile(GenerationContext context, string outputDir, bool forceOverwrite, ILogger logger)
    {
        // 生成代码文件
        if (context.DataSetType == "matrix")
        {
            var dataRowTemplate = new DataMatrixTemplate(context);
            logger.Debug(WriteToFile(outputDir, context.RealClassName + ".cs", dataRowTemplate.TransformText(), forceOverwrite));
        }
        else
        {
            var dataRowTemplate = new DataTableTemplate(context);
            logger.Debug(WriteToFile(outputDir, context.RealClassName + ".cs", dataRowTemplate.TransformText(), forceOverwrite));
        }
    }

    static string NormalizeNewLines(string content)
    {
        // The T4 generated code may be text with mixed line ending types. (CR + CRLF)
        // We need to normalize the line ending type in each Operating Systems. (e.g. Windows=CRLF, Linux/macOS=LF)
        return content.Replace("\r\n", "\n");
    }

    static string WriteToFile(string directory, string fileName, string content, bool forceOverwrite)
    {
        var path = Path.Combine(directory, fileName);
        var contentBytes = Encoding.UTF8.GetBytes(NormalizeNewLines(content));

        // If the generated content is unchanged, skip the write.
        if (!forceOverwrite && File.Exists(path))
        {
            if (new FileInfo(path).Length == contentBytes.Length && contentBytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)))
            {
                return $"  > Generate {fileName} to: {path} (Skipped)";
            }
        }

        File.WriteAllBytes(path, contentBytes);
        return $"  > Generate {fileName} to: {path}";
    }
}
