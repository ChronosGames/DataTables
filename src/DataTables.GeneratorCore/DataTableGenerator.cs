using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Text.RegularExpressions;
using ExcelDataReader;
using static System.Net.Mime.MediaTypeNames;

namespace DataTables.GeneratorCore
{
    public sealed class DataTableGenerator
    {
        private const int HeadRowCount = 4;
        private static readonly Regex NameRegex = new Regex(@"^[A-Za-z][A-Za-z0-9_]*$");

        public void GenerateFile(string inputDirectory, string codeOutputDir, string dataOutputDir, string usingNamespace, string prefixClassName, string filterColumnTags, bool forceOverwrite, Action<string> logger)
        {
            // By default, ExcelDataReader throws a NotSupportedException "No data is available for encoding 1252." on .NET Core.
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            prefixClassName ??= "";
            var list = new List<GenerationContext>();

            // Collect
            if (inputDirectory.EndsWith(".csproj"))
            {
                throw new InvalidOperationException("Path must be directory but it is csproj. inputDirectory:" + inputDirectory);
            }

            foreach (var item in Directory.GetFiles(inputDirectory, "*.xlsx", SearchOption.AllDirectories))
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

                // 判断是否存在配置表变更（以修改时间为准），若不存在则直接跳过
                if (!forceOverwrite)
                {
                    var excelFileInfo = new FileInfo(context.InputFilePath);

                    var targetFilePath = Path.Combine(dataOutputDir, context.RealClassName + ".bytes");
                    if (File.Exists(targetFilePath))
                    {
                        var targetFileInfo = new FileInfo(targetFilePath);
                        if (excelFileInfo.LastWriteTime < targetFileInfo.LastWriteTime)
                        {
                            logger(string.Format("Generate Excel File: {0}, Sheet={1} (skiped)", context.InputFilePath, context.SheetName));
                            continue;
                        }
                    }
                }

                logger(string.Format("Generate Excel File: {0}, Sheet:{1}", context.InputFilePath, context.SheetName));

                // 生成代码文件
                GenerateCodeFile(context, codeOutputDir, forceOverwrite, logger);
                
                // 生成二进制文件
                GenerateDataFile(context, dataOutputDir, forceOverwrite, logger);
            }

            logger("Generate Manager Files:");

            // 生成DataTableManagerExtension代码文件(放在未尾确保类名前缀会正确附加)
            var dataTableManagerExtensionTemplate = new DataTableManagerExtensionTemplate()
            {
                Namespace = usingNamespace,
                DataRowPrefix = prefixClassName,
                DataRowTypeName = list.Select(x => x.ClassName).ToArray(),
            };
            logger(WriteToFile(codeOutputDir, "DataTableManagerExtension.cs", dataTableManagerExtensionTemplate.TransformText(), forceOverwrite));
        }

        IEnumerable<GenerationContext> CreateGenerationContext(string filePath, string filterColumnTags, Action<string> logger)
        {
            using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read))
            {
                // Auto-detect format, supports:
                //  - Binary Excel files (2.0-2003 format; *.xls)
                //  - OpenXml Excel files (2007 format; *.xlsx, *.xlsb)
                using (var reader = ExcelReaderFactory.CreateReader(stream))
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
                                Indexs = new List<string[]>(),
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
                                if (ParseSheetInfoRow(context, rowReader))
                                {
                                    rowReader.Read();
                                    ParseFieldCommentRow(context, rowReader, filterColumnTags);

                                    rowReader.Read();
                                    ParseFieldNameRow(context, rowReader);

                                    rowReader.Read();
                                    ParseFieldTypeRow(context, rowReader);
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
                        ParseDataSet(context, result.Tables[pair.Key]);

                        yield return context;
                    }
                }

                yield break;
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
                    switch (properties[0])
                    {
                        case "Title":
                            context.Title = properties[1];
                            break;
                        case "Class":
                            context.ClassName = properties[1];
                            break;
                        case "EnableTagsFilter":
                            context.EnableTagsFilter = bool.Parse(properties[1]);
                            break;
                        case "Index":
                            context.Indexs.Add(properties[1].Split('&'));
                            break;
                    }
                }
                else if (properties.Length == 1)
                {
                    switch (properties[0])
                    {
                        case "EnableTagsFilter":
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

                context.Properties[i] = new Property();
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
                    return $"# Generate {fileName} to: {path} (Skipped)";
                }
            }

            File.WriteAllBytes(path, contentBytes);
            return $"# Generate {fileName} to: {path}";
        }

        static void GenerateDataFile(GenerationContext context, string outputDir, bool forceOverwrite, Action<string> logger)
        {
            string binaryDataFileName = Path.Combine(outputDir, context.RealClassName + ".bytes");
            if (!DataTableProcessor.GenerateDataFile(context, binaryDataFileName, logger) && File.Exists(binaryDataFileName))
            {
                File.Delete(binaryDataFileName);
            }
        }
    }
}
