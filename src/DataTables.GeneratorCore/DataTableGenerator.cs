using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace DataTables.GeneratorCore;

public sealed class DataTableGenerator
{
    public void GenerateFile(string inputDirectory, string codeOutputDir, string dataOutputDir, string usingNamespace, string prefixClassName, string importNamespaces, string filterColumnTags, bool forceOverwrite, Action<string> logger)
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
        if (inputDirectory.EndsWith(".csproj"))
        {
            throw new InvalidOperationException("Path must be directory but it is csproj. inputDirectory:" + inputDirectory);
        }

        var filePaths = Directory.EnumerateFiles(inputDirectory, "*.*", SearchOption.AllDirectories)
                    .Where(s => s.EndsWith(".xlsx") || s.EndsWith(".xlsb") || s.EndsWith(".xls") || s.EndsWith(".csv")).ToArray();

        if (filePaths.Count() == 0)
        {
            throw new InvalidOperationException("Not found Excel files, inputDir:" + inputDirectory);
        }

        if (!Directory.Exists(codeOutputDir))
        {
            Directory.CreateDirectory(codeOutputDir);
        }

        if (!Directory.Exists(dataOutputDir))
        {
            Directory.CreateDirectory(dataOutputDir);
        }

        Parallel.ForEach(filePaths, filePath => GenerateExcel(filePath,
            inputDir: inputDirectory,
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

        SortedDictionary<string, string[]> dataTables = new SortedDictionary<string, string[]>();
        foreach (var context in list)
        {
            if (dataTables.ContainsKey(context.ClassName))
            {
                continue;
            }

            // 收集全部的子表
            if (!string.IsNullOrEmpty(context.Child))
            {
                context.Children = list.Where(x => x.ClassName == context.ClassName && !string.IsNullOrEmpty(x.Child)).Select(x => x.Child).ToArray();
            }

            // 记录子表名称
            dataTables.Add(context.ClassName, context.Children);
        }

        // 生成DataTableManagerExtension代码文件(放在未尾确保类名前缀会正确附加)
        var dataTableManagerExtensionTemplate = new DataTableManagerExtensionTemplate()
        {
            Namespace = usingNamespace,
            DataRowPrefix = prefixClassName,
            DataTables = dataTables,
        };
        logger(WriteToFile(codeOutputDir, "DataTableManagerExtension.cs", dataTableManagerExtensionTemplate.TransformText(), forceOverwrite));

        logger(string.Empty);
        logger("===========================================================");
        logger($"数据表导出完成: {list.Count(x => !x.Failed && !x.Skiped)} 成功，{list.Count(x => x.Failed)} 失败，{list.Count(x => x.Skiped)} 已跳过");
    }

    private static void GenerateExcel(string filePath, string usingNamespace, string prefixClassName, string[] usingStrings, string filterColumnTags, string inputDir, string codeOutputDir, string dataOutputDir, bool forceOverwrite, ConcurrentBag<GenerationContext> list, Action<string> log)
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

                    logger.Debug("Generate Excel File: [{0}]({1})", filePath.Replace(inputDir, "").Trim('\\'), context.SheetName);

                    using (var processor = new DataTableProcessor(context, filterColumnTags))
                    {
                        // 初始化GenerateContext
                        processor.CreateGenerateContext(sheet);
                        if (!processor.ValidateGenerateContext())
                        {
                            continue;
                        }

                        list.Add(context);

                        // 收集全部的子表
                        if (!string.IsNullOrEmpty(context.Child))
                        {
                            context.Children = list.Where(x => x.ClassName == context.ClassName && !string.IsNullOrEmpty(x.Child)).Select(x => x.Child).ToArray();
                        }

                        // 生成代码文件
                        GenerateCodeFile(context, codeOutputDir, forceOverwrite, logger);

                        // 生成数据文件
                        processor.GenerateDataFile(filePath, dataOutputDir, forceOverwrite, sheet, logger);
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
        var dataRowTemplate = new DataRowTemplate(context)
        {
            Using = string.Join(Environment.NewLine, context.UsingStrings),
        };

        logger.Debug(WriteToFile(outputDir, context.RealClassName + ".cs", dataRowTemplate.TransformText(), forceOverwrite));
    }

    static string NormalizeNewLines(string content)
    {
        // The T4 generated code may be text with mixed line ending types. (CR + CRLF)
        // We need to normalize the line ending type in each Operating Systems. (e.g. Windows=CRLF, Linux/macOS=LF)
        return content.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
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
