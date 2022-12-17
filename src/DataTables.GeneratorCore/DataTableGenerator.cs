using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ExcelDataReader;

namespace DataTables.GeneratorCore
{
    public sealed class DataTableGenerator
    {
        private static readonly Regex EndWithNumberRegex = new Regex(@"\d+$");
        private static readonly Regex NameRegex = new Regex(@"^[A-Z][A-Za-z0-9_]*$");

        public void GenerateFile(string inputDirectory, string codeOutputDir, string dataOutputDir, string usingNamespace, string prefixClassName, bool forceOverwrite, Action<string> logger)
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
                list.AddRange(CreateGenerationContext(item, logger));
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

                // 生成代码文件
                GenerateCodeFile(context, codeOutputDir, forceOverwrite, logger);
                
                // 生成二进制文件
                GenerateDataFile(context, dataOutputDir, forceOverwrite, logger);
            }
        }

        IEnumerable<GenerationContext> CreateGenerationContext(string filePath, Action<string> logger)
        {
            using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read))
            {
                // Auto-detect format, supports:
                //  - Binary Excel files (2.0-2003 format; *.xls)
                //  - OpenXml Excel files (2007 format; *.xlsx, *.xlsb)
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    var result = reader.AsDataSet(new ExcelDataSetConfiguration()
                    {
                        // Gets or sets a value indicating whether to set the DataColumn.DataType 
                        // property in a second pass.
                        UseColumnDataType = true,

                        // Gets or sets a callback to determine whether to include the current sheet
                        // in the DataSet. Called once per sheet before ConfigureDataTable.
                        FilterSheet = (tableReader, sheetIndex) => true,

                        // Gets or sets a callback to obtain configuration options for a DataTable. 
                        ConfigureDataTable = (tableReader) => new ExcelDataTableConfiguration()
                        {
                            // Gets or sets a value indicating the prefix of generated column names.
                            EmptyColumnNamePrefix = "Column",

                            // Gets or sets a value indicating whether to use a row from the 
                            // data as column names.
                            UseHeaderRow = false,

                            // Gets or sets a callback to determine which row is the header row. 
                            // Only called when UseHeaderRow = true.
                            ReadHeaderRow = (rowReader) =>
                            {
                                // F.ex skip the first row and use the 2nd row as column headers:
                                //rowReader.Read();
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
                                // Console.WriteLine($"{rowReader.GetValue(columnIndex)}, Depth={rowReader.Depth}, FieldCount={rowReader.FieldCount}, RowCount={rowReader.RowCount}");

                                var value = rowReader.GetValue(columnIndex);
                                if (value == null)
                                {
                                    return false;
                                }

                                if (string.IsNullOrEmpty(value.ToString()))
                                {
                                    return false;
                                }

                                return !value.ToString().Trim().StartsWith("#");
                            }
                        }
                    });

                    // The result of each spreadsheet is in result.Tables
                    for (int i = 0; i < result.Tables.Count; i++)
                    {
                        var table = result.Tables[i];
                        if (!CheckRawData(table))
                        {
                            logger($"配置表格式不合法: InputFile={Path.GetFileNameWithoutExtension(filePath)}, Sheet={table.TableName}");
                            continue;
                        }

                        var context = GenerateGenerationContext(filePath, table);
                        yield return context;
                    }
                }

                yield break;
            }
        }

        static bool CheckRawData(DataTable table)
        {
            if (table.Rows.Count < 3)
            {
                return false;
            }

            if (!NameRegex.IsMatch(table.TableName))
            {
                return false;
            }

            for (int i = 0; i < table.Columns.Count; i++)
            {
                var column = table.Rows[1][i];
                if (column == null)
                {
                    continue;
                }

                var columnValue = column.ToString();
                if (!NameRegex.IsMatch(columnValue))
                {
                    return false;
                }
            }

            return true;
        }

        static GenerationContext GenerateGenerationContext(string filePath, DataTable table)
        {
            var context = new GenerationContext
            {
                FileName = Path.GetFileNameWithoutExtension(filePath),
                SheetName = table.TableName,
                UsingStrings = Array.Empty<string>(),
                InputFilePath = filePath,
                Properties = GenerateDataTablePropertyArray(table),
            };

            // 拼装值数据
            var (rowCount, columnCount, cells) = GenerateDataTableDataArray(context.Properties, table);
            context.RowCount = rowCount;
            context.ColumnCount = columnCount;
            context.Cells = cells;

            // Console.WriteLine($"{table.TableName}, Rows={table.Rows.Count}");

            return context;
        }

        static void GenerateCodeFile(GenerationContext context, string outputDir, bool forceOverwrite, Action<string> logger)
        {
            // 生成代码文件
            var codeTemplate = new CodeTemplate()
            {
                Using = string.Join(Environment.NewLine, context.UsingStrings),
                GenerationContext = context,
            };

            logger(WriteToFile(outputDir, context.ClassName + ".cs", codeTemplate.TransformText(), forceOverwrite));
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
                    return $"Generate {fileName} to: {path} (Skipped)";
                }
            }

            File.WriteAllBytes(path, contentBytes);
            return $"Generate {fileName} to: {path}";
        }

        static void GenerateDataFile(GenerationContext context, string outputDir, bool forceOverwrite, Action<string> logger)
        {
            string binaryDataFileName = Path.Combine(outputDir, context.ClassName + ".bin");
            if (!DataTableProcessor.GenerateDataFile(context, binaryDataFileName, logger) && File.Exists(binaryDataFileName))
            {
                File.Delete(binaryDataFileName);
            }
        }

        private static Property[] GenerateDataTablePropertyArray(DataTable table)
        {
            var properties = new Property[table.Columns.Count];

            for (int i = 0; i < table.Columns.Count; i++)
            {
                var property = new Property()
                {
                    Comment = table.Rows[0][i].ToString().Trim(),
                    Name = table.Rows[1][i].ToString().Trim(),
                    TypeName = table.Rows[2][i].ToString().Trim(),
                };

                properties[i] = property;
            }

            return properties;
        }

        private static (int, int, object[,]) GenerateDataTableDataArray(Property[] properties, DataTable table)
        {
            int rowCount = table.Rows.Count - 3;
            int columnCount = table.Columns.Count;
            object[,] cells = new object[rowCount, columnCount];

            for (int i = 0; i < rowCount; i++)
            {
                for (int j = 0; j < columnCount; j++)
                {
                    cells[i, j] = table.Rows[i + 3][j];
                }
            }

            return (rowCount, columnCount, cells);
        }
    }
}
