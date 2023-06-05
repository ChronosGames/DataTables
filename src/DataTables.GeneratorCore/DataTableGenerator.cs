using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ExcelDataReader;
using ExcelDataReader.Exceptions;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace DataTables.GeneratorCore;

public sealed class DataTableGenerator
{
    private const int HeadRowCount = 4;
    private static readonly Regex NameRegex = new Regex(@"^[A-Za-z][A-Za-z0-9_]*$");

    public void GenerateFile(string inputDirectory, string codeOutputDir, string dataOutputDir, string usingNamespace, string prefixClassName, string importNamespaces, string filterColumnTags, bool forceOverwrite, Action<string> logger)
    {
        // By default, ExcelDataReader throws a NotSupportedException "No data is available for encoding 1252." on .NET Core.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        prefixClassName ??= string.Empty;
        var list = new List<GenerationContext>();

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

        var files = Directory.EnumerateFiles(inputDirectory, "*.*", SearchOption.AllDirectories)
                    .Where(s => s.EndsWith(".xlsx") || s.EndsWith(".xlsb") || s.EndsWith(".xls") || s.EndsWith(".csv"));

        foreach (var item in files)
        {
            list.AddRange(CreateGenerationContext(item, filterColumnTags, logger));
        }
        // list.Sort((a, b) => string.Compare(a.FileName + a.SheetName, a.FileName + b.SheetName, StringComparison.Ordinal));

        if (list.Count == 0)
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

        foreach (var context in list)
        {
            // 全局属性赋值
            context.Namespace = usingNamespace;
            context.PrefixClassName = prefixClassName;
            context.UsingStrings = usingStrings;

            // 判断是否存在配置表变更（以修改时间为准），若不存在则直接跳过
            if (!forceOverwrite)
            {
                var processPath = Process.GetCurrentProcess().MainModule.FileName;
                var processLastWriteTime = File.GetLastWriteTime(processPath);
                var excelLastWriteTime = File.GetLastWriteTime(context.InputFilePath);

                var targetFilePath = Path.Combine(dataOutputDir, context.GetDataOutputFilePath());
                if (File.Exists(targetFilePath))
                {
                    var dataLastWriteTime = File.GetLastWriteTime(targetFilePath);
                    if (dataLastWriteTime > excelLastWriteTime && dataLastWriteTime > processLastWriteTime)
                    {
                        // 标记为跳过
                        context.Skiped = true;

                        logger(string.Format("Generate Excel File: [{0}]({1}) (skiped)", context.InputFilePath.Replace(inputDirectory, "").Trim('\\'), context.SheetName));
                        continue;
                    }
                }
            }

            logger(string.Format("Generate Excel File: [{0}]({1})", context.InputFilePath.Replace(inputDirectory, "").Trim('\\'), context.SheetName));

            // 加载首行单元格的批注信息
            LoadFirstRowCellNote(context);

            // 收集全部的子表
            if (!string.IsNullOrEmpty(context.Child))
            {
                context.Children = list.Where(x => x.ClassName == context.ClassName && !string.IsNullOrEmpty(x.Child)).Select(x => x.Child).ToArray();
            }

            // 生成代码文件
            GenerateCodeFile(context, codeOutputDir, forceOverwrite, logger);

            // 生成二进制文件
            GenerateDataFile(context, dataOutputDir, forceOverwrite, logger);
        }

        logger("Generate Manager Files:");

        Dictionary<string, string[]> dataTables = new Dictionary<string, string[]>();
        foreach (var context in list)
        {
            if (dataTables.ContainsKey(context.ClassName))
            {
                continue;
            }

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

    IEnumerable<GenerationContext> CreateGenerationContext(string filePath, string filterColumnTags, Action<string> logger)
    {
        using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read))
        {
            // Auto-detect format, supports:
            //  - Binary Excel files (2.0-2003 format; *.xls)
            //  - OpenXml Excel files (2007 format; *.xlsx, *.xlsb)
            IExcelDataReader reader;
            try
            {
                reader = ExcelReaderFactory.CreateReader(stream);
            }
            catch (HeaderException he)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                logger($"Open '{filePath}' failure, exception is '{he.Message}'.");
                Console.ResetColor();
                yield break;
            }

            try
            {
                var contexts = new Dictionary<string, GenerationContext>();
                var result = reader.AsDataSet(new ExcelDataSetConfiguration()
                {
                    // Gets or sets a value indicating whether to set the DataColumn.DataType 
                    // property in a second pass.
                    UseColumnDataType = true,

                    // Gets or sets a callback to determine whether to include the current sheet
                    // in the DataSet. Called once per sheet before ConfigureDataTable.
                    FilterSheet = (tableReader, sheetIndex) =>
                    {
                        var isCommentSheet = tableReader.Name.StartsWith("#", StringComparison.Ordinal);
                        if (isCommentSheet)
                        {
                            return false;
                        }

                        if (tableReader.RowCount < HeadRowCount)
                        {
                            return false;
                        }

                        var context = new GenerationContext
                        {
                            FileName = Path.GetFileNameWithoutExtension(filePath),
                            UsingStrings = Array.Empty<string>(),

                            InputFilePath = filePath,
                            SheetName = tableReader.Name,
                        };

                        contexts.Add(tableReader.Name, context);

                        return true;
                    },

                    // Gets or sets a callback to obtain configuration options for a DataTable. 
                    ConfigureDataTable = (tableReader) => new ExcelDataTableConfiguration()
                    {
                        // Gets or sets a value indicating the prefix of generated column names.
                        EmptyColumnNamePrefix = "Column",

                        // Gets or sets a value indicating whether to use a row from the 
                        // data as column names.
                        UseHeaderRow = true,

                        // Gets or sets a callback to determine which row is the header row. 
                        // Only called when UseHeaderRow = true.
                        ReadHeaderRow = (rowReader) =>
                        {
                            var context = contexts[rowReader.Name];

                            // F.ex skip the first row and use the 2nd row as column headers:
                            //rowReader.Read();
                            try
                            {
                                if (ParseSheetInfoRow(context, rowReader))
                                {
                                    rowReader.Read();
                                    ParseFieldCommentRow(context, rowReader, filterColumnTags);

                                    rowReader.Read();
                                    ParseFieldNameRow(context, rowReader);

                                    rowReader.Read();
                                    ParseFieldTypeRow(context, rowReader);
                                }
                            }
                            catch (Exception e)
                            {
                                throw new Exception($"Parse {filePath}'s '{rowReader.Name}' exception.", e);
                            }
                        },

                        // Gets or sets a callback to determine whether to include the 
                        // current row in the DataTable.
                        FilterRow = (rowReader) =>
                        {
                            // Console.WriteLine($"{rowReader.GetValue(0)}, Depth={rowReader.Depth}, FieldCount={rowReader.FieldCount}, RowCount={rowReader.RowCount}");
                            var value = rowReader.GetValue(0);
                            if (value == null)
                            {
                                return false;
                            }

                            return !value.ToString().Trim().StartsWith("#");
                        },

                        // Gets or sets a callback to determine whether to include the specific
                        // column in the DataTable. Called once per column after reading the 
                        // headers.
                        FilterColumn = (rowReader, columnIndex) =>
                        {
                            var context = contexts[rowReader.Name];
                            if (context.Properties == null)
                            {
                                return false;
                            }

                            var property = context.Properties[columnIndex];
                            return property != null;
                        }
                    }
                });

                foreach (var pair in contexts)
                {
                    // 解析数据
                    var context = pair.Value;
                    if (string.IsNullOrEmpty(context.ClassName))
                    {
                        continue;
                    }

                    // 忽略无属性的Sheet
                    if (context.Properties == null)
                    {
                        continue;
                    }

                    // 移除空的Property元素
                    RemoveEmptyProperties(pair.Value);

                    // 忽略属性列表个数为零的Sheet
                    if (context.Properties.Length == 0)
                    {
                        continue;
                    }

                    // 解析数据
                    try
                    {
                        ParseDataSet(context, result.Tables[pair.Key]);
                    }
                    catch (Exception e)
                    {
                        throw new Exception($"Parse {filePath}'s {pair.Value.SheetName} exception.", e);
                    }

                    yield return context;
                }
            }
            finally
            {
                reader.Dispose();
            }
        }

        yield break;
    }

    private static void LoadFirstRowCellNote(GenerationContext context)
    {
        using (var stream = new FileStream(context.InputFilePath, FileMode.Open))
        {
            stream.Position = 0;
            using (XSSFWorkbook xssWorkbook = new XSSFWorkbook(stream))
            {
                var sheet = xssWorkbook.GetSheet(context.SheetName);

                IRow row;
                ICell cell;
                for (int i = 0; i < 100; i++)
                {
                    row = sheet.GetRow(i);
                    if (row.GetCell(0).StringCellValue.Trim().StartsWith("#"))
                    {
                        continue;
                    }

                    for (int j = i + 1; j < 100; j++)
                    {
                        row = sheet.GetRow(j);
                        cell = row.GetCell(0);
                        if (cell.CellType == CellType.String && cell.StringCellValue.Trim().StartsWith("#"))
                        {
                            continue;
                        }

                        for (int k = 0; k < row.Cells.Count; k++)
                        {
                            cell = row.Cells[k];
                            if (cell.CellComment == null)
                            {
                                continue;
                            }

                            if (string.IsNullOrEmpty(cell.CellComment.String.String))
                            {
                                continue;
                            }

                            var field = context.Properties.FirstOrDefault(x => x.Index == k);
                            if (field != null)
                            {
                                field.Note = cell.CellComment.String.String;
                            }
                        }

                        break;
                    }

                    break;
                }
            }
        }
    }

    private static void RemoveEmptyProperties(GenerationContext context)
    {
        var properties = context.Properties;
        int i = 0, j = 0;
        for (; i < properties.Length; i++)
        {
            if (properties[i] != null)
            {
                continue;
            }

            // 寻找下一个非空的
            var found = -1;
            for (j = Math.Max(j, i + 1); j < properties.Length; j++)
            {
                if (properties[j] != null)
                {
                    found = j;
                    break;
                }
            }

            if (found != -1)
            {
                properties[i] = properties[found];
                properties[found] = null;
            }
            else
            {
                break;
            }
        }

        if (i != properties.Length)
        {
            Array.Resize(ref properties, i);
            context.Properties = properties;
        }
    }

    #region 数据表解析过程

    private static bool ContainTags(string text, string tags)
    {
        for (int i = 0; i < text.Length; i++)
        {
            if (tags.Contains(text[i]))
            {
                return true;
            }
        }

        return false;
    }

    // 解析第一行的表头信息
    private static bool ParseSheetInfoRow(GenerationContext context, IExcelDataReader reader)
    {
        var value = reader.GetValue(0);
        if (value == null)
        {
            return false;
        }

        var arr = reader.GetString(0).Split(',');
        foreach (var pair in arr)
        {
            var properties = pair.Split('=');
            if (properties.Length == 2)
            {
                switch (properties[0].Trim().ToLower())
                {
                    case "title":
                        context.Title = properties[1].Trim();
                        break;
                    case "class":
                        context.ClassName = properties[1].Trim();
                        break;
                    case "enabletagsfilter":
                        context.EnableTagsFilter = bool.Parse(properties[1].Trim());
                        break;
                    case "index":
                        context.Indexs.Add(properties[1].Trim().Split('&'));
                        break;
                    case "group":
                        context.Groups.Add(properties[1].Trim().Split('&'));
                        break;
                    case "child":
                        context.Child = properties[1].Trim();
                        break;
                }
            }
            else if (properties.Length == 1)
            {
                switch (properties[0].Trim().ToLower())
                {
                    case "enabletagsfilter":
                        context.EnableTagsFilter = true;
                        break;
                }
            }
        }

        return !string.IsNullOrEmpty(context.ClassName);
    }

    private static void ParseFieldCommentRow(GenerationContext context, IExcelDataReader reader, string filterColumnTags)
    {
        context.Properties = new Property[reader.FieldCount];

        for (int i = 0; i < reader.FieldCount; i++)
        {
            // 修正列名行文本为空时解析报错
            if (reader.GetValue(i) == null)
            {
                continue;
            }

            var text = reader.GetString(i).Trim();
            if (text.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            // 是否允许导出
            if (context.EnableTagsFilter)
            {
                var index = text.LastIndexOf('@');
                if (index != -1)
                {
                    if (!string.IsNullOrEmpty(filterColumnTags) && !ContainTags(text.Substring(index + 1).ToUpper(), filterColumnTags.ToUpper()))
                    {
                        continue;
                    }

                    text = text.Substring(0, index);
                }
            }

            context.Properties[i] = new Property(i);
            context.Properties[i].Comment = text;
        }
    }

    private static void ParseFieldNameRow(GenerationContext context, IExcelDataReader reader)
    {
        for (int i = 0; i < reader.FieldCount; i++)
        {
            if (context.Properties[i] == null)
            {
                continue;
            }

            var text = reader.GetString(i).Trim();
            if (!NameRegex.IsMatch(text))
            {
                context.Properties[i] = null;
                continue;
            }

            context.Properties[i].Name = text;
        }
    }

    private static void ParseFieldTypeRow(GenerationContext context, IExcelDataReader reader)
    {
        for (int i = 0; i < reader.FieldCount; i++)
        {
            if (context.Properties[i] == null)
            {
                continue;
            }

            context.Properties[i].TypeName = reader.GetString(i).Trim();
        }
    }

    private static void ParseDataSet(GenerationContext context, DataTable table)
    {
        int rowCount = table.Rows.Count;
        int columnCount = table.Columns.Count;
        Debug.Assert(columnCount == context.Properties.Length, "列个数不一致");

        object[,] cells = new object[rowCount, columnCount];

        for (int i = 0; i < rowCount; i++)
        {
            for (int j = 0; j < columnCount; j++)
            {
                cells[i, j] = table.Rows[i][j];
            }
        }

        context.RowCount = rowCount;
        context.ColumnCount = columnCount;
        context.Cells = cells;
    }

    #endregion

    static void GenerateCodeFile(GenerationContext context, string outputDir, bool forceOverwrite, Action<string> logger)
    {
        // 生成代码文件
        var dataRowTemplate = new DataRowTemplate()
        {
            Using = string.Join(Environment.NewLine, context.UsingStrings),
            GenerationContext = context,
        };

        logger(WriteToFile(outputDir, context.RealClassName + ".cs", dataRowTemplate.TransformText(), forceOverwrite));
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

    static void GenerateDataFile(GenerationContext context, string outputDir, bool forceOverwrite, Action<string> logger)
    {
        string binaryDataFileName = Path.Combine(outputDir, context.GetDataOutputFilePath());
        if (!DataTableProcessor.GenerateDataFile(context, binaryDataFileName, logger))
        {
            // 记录出错的情况
            context.Failed = true;

            if (File.Exists(binaryDataFileName))
            {
                File.Delete(binaryDataFileName);
            }
        }
    }
}
