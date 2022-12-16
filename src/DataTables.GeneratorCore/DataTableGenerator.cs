using System;
using System.Collections.Generic;
using System.IO;
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
                list.AddRange(CreateGenerationContext(item));
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

        IEnumerable<GenerationContext> CreateGenerationContext(string filePath)
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
                                return true;
                            },

                            // Gets or sets a callback to determine whether to include the specific
                            // column in the DataTable. Called once per column after reading the 
                            // headers.
                            FilterColumn = (rowReader, columnIndex) =>
                            {
                                return true;
                            }
                        }
                    });

                    // The result of each spreadsheet is in result.Tables
                    for (int i = 0; i < result.Tables.Count; i++)
                    {
                        var table = result.Tables[i];
                        if (table.Rows.Count <= 3)
                        {
                            continue;
                        }

                        var context = new GenerationContext
                        {
                            FileName = Path.GetFileNameWithoutExtension(filePath),
                            SheetName = table.TableName,
                            Properties = new Property[table.Columns.Count],
                            UsingStrings = Array.Empty<string>(),
                            InputFilePath = filePath,
                        };

                        // 拼装Meta数据
                        for (int j = 0; j < table.Columns.Count; j++)
                        {
                            context.Properties[j] = new Property
                            {
                                Name = table.Rows[1][j].ToString().Trim(),
                                Type = table.Rows[2][j].ToString().Trim(),
                                Comment = table.Rows[0][j].ToString().Trim(),
                            };
                        }

                        // 拼装值数据
                        context.CellRow = table.Rows.Count - 3;
                        context.CellColumn = table.Columns.Count;
                        context.Cells = new object[context.CellRow, context.CellColumn];
                        for (int m = 3; m < table.Rows.Count; m++)
                        {
                            for (int n = 0; n < table.Columns.Count; n++)
                            {
                                context.Cells[m - 3, n] = table.Rows[m][n];
                            }
                        }

                        Console.WriteLine($"{table.TableName}, Rows={table.Rows.Count}");
                        yield return context;
                    }
                }

                yield break;
            }
        }

        static void GenerateCodeFile(GenerationContext context, string outputDir, bool forceOverwrite, Action<string> logger)
        {
            // 生成代码文件
            var codeTemplate = new CodeTemplate()
            {
                Using = string.Join(Environment.NewLine, context.UsingStrings),
                GenerationContext = context,
            };

            logger(WriteToFile(outputDir, context.ClassName + "Code.cs", codeTemplate.TransformText(), forceOverwrite));
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

        }

        //public static DataTableProcessor CreateDataTableProcessor(string dataTableName)
        //{
        //    return new DataTableProcessor(Utility.Path.GetRegularPath(Path.Combine(DataTablePath, dataTableName + ".txt")), Encoding.GetEncoding("GB2312"), 1, 2, null, 3, 4, 1);
        //}

        //public static bool CheckRawData(DataTableProcessor dataTableProcessor, string dataTableName)
        //{
        //    for (int i = 0; i < dataTableProcessor.RawColumnCount; i++)
        //    {
        //        string name = dataTableProcessor.GetName(i);
        //        if (string.IsNullOrEmpty(name) || name == "#")
        //        {
        //            continue;
        //        }

        //        if (!NameRegex.IsMatch(name))
        //        {
        //            Debug.LogWarning(string.Format("Check raw data failure. DataTableName='{0}' Name='{1}'", dataTableName, name));
        //            return false;
        //        }
        //    }

        //    return true;
        //}

        //public static void GenerateDataFile(DataTableProcessor dataTableProcessor, string dataTableName)
        //{
        //    string binaryDataFileName = Utility.Path.GetRegularPath(Path.Combine(DataTablePath, dataTableName + ".bytes"));
        //    if (!dataTableProcessor.GenerateDataFile(binaryDataFileName) && File.Exists(binaryDataFileName))
        //    {
        //        File.Delete(binaryDataFileName);
        //    }
        //}

        private static string GenerateDataTableProperties(DataTableProcessor dataTableProcessor)
        {
            StringBuilder stringBuilder = new StringBuilder();
            bool firstProperty = true;
            for (int i = 0; i < dataTableProcessor.RawColumnCount; i++)
            {
                if (dataTableProcessor.IsCommentColumn(i))
                {
                    // 注释列
                    continue;
                }

                if (dataTableProcessor.IsIdColumn(i))
                {
                    // 编号列
                    continue;
                }

                if (firstProperty)
                {
                    firstProperty = false;
                }
                else
                {
                    stringBuilder.AppendLine().AppendLine();
                }

                stringBuilder
                    .AppendLine("        /// <summary>")
                    .AppendFormat("        /// 获取{0}。", dataTableProcessor.GetComment(i)).AppendLine()
                    .AppendLine("        /// </summary>")
                    .AppendFormat("        public {0} {1}", dataTableProcessor.GetLanguageKeyword(i), dataTableProcessor.GetName(i)).AppendLine()
                    .AppendLine("        {")
                    .AppendLine("            get;")
                    .AppendLine("            private set;")
                    .Append("        }");
            }

            return stringBuilder.ToString();
        }

        private static string GenerateDataTableParser(DataTableProcessor dataTableProcessor)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder
                .AppendLine("        public override bool ParseDataRow(string dataRowString, object userData)")
                .AppendLine("        {")
                .AppendLine("            string[] columnStrings = dataRowString.Split(DataTableExtension.DataSplitSeparators);")
                .AppendLine("            for (int i = 0; i < columnStrings.Length; i++)")
                .AppendLine("            {")
                .AppendLine("                columnStrings[i] = columnStrings[i].Trim(DataTableExtension.DataTrimSeparators);")
                .AppendLine("            }")
                .AppendLine()
                .AppendLine("            int index = 0;");

            for (int i = 0; i < dataTableProcessor.RawColumnCount; i++)
            {
                if (dataTableProcessor.IsCommentColumn(i))
                {
                    // 注释列
                    stringBuilder.AppendLine("            index++;");
                    continue;
                }

                if (dataTableProcessor.IsIdColumn(i))
                {
                    // 编号列
                    stringBuilder.AppendLine("            m_Id = int.Parse(columnStrings[index++]);");
                    continue;
                }

                if (dataTableProcessor.IsSystem(i))
                {
                    string languageKeyword = dataTableProcessor.GetLanguageKeyword(i);
                    if (languageKeyword == "string")
                    {
                        stringBuilder.AppendFormat("            {0} = columnStrings[index++];", dataTableProcessor.GetName(i)).AppendLine();
                    }
                    else
                    {
                        stringBuilder.AppendFormat("            {0} = {1}.Parse(columnStrings[index++]);", dataTableProcessor.GetName(i), languageKeyword).AppendLine();
                    }
                }
                else
                {
                    stringBuilder.AppendFormat("            {0} = DataTableExtension.Parse{1}(columnStrings[index++]);", dataTableProcessor.GetName(i), dataTableProcessor.GetType(i).Name).AppendLine();
                }
            }

            stringBuilder.AppendLine()
                .AppendLine("            GeneratePropertyArray();")
                .AppendLine("            return true;")
                .AppendLine("        }")
                .AppendLine()
                .AppendLine("        public override bool ParseDataRow(byte[] dataRowBytes, int startIndex, int length, object userData)")
                .AppendLine("        {")
                .AppendLine("            using (MemoryStream memoryStream = new MemoryStream(dataRowBytes, startIndex, length, false))")
                .AppendLine("            {")
                .AppendLine("                using (BinaryReader binaryReader = new BinaryReader(memoryStream, Encoding.UTF8))")
                .AppendLine("                {");

            for (int i = 0; i < dataTableProcessor.RawColumnCount; i++)
            {
                if (dataTableProcessor.IsCommentColumn(i))
                {
                    // 注释列
                    continue;
                }

                if (dataTableProcessor.IsIdColumn(i))
                {
                    // 编号列
                    stringBuilder.AppendLine("                    m_Id = binaryReader.Read7BitEncodedInt32();");
                    continue;
                }

                string languageKeyword = dataTableProcessor.GetLanguageKeyword(i);
                if (languageKeyword == "int" || languageKeyword == "uint" || languageKeyword == "long" || languageKeyword == "ulong")
                {
                    stringBuilder.AppendFormat("                    {0} = binaryReader.Read7BitEncoded{1}();", dataTableProcessor.GetName(i), dataTableProcessor.GetType(i).Name).AppendLine();
                }
                else
                {
                    stringBuilder.AppendFormat("                    {0} = binaryReader.Read{1}();", dataTableProcessor.GetName(i), dataTableProcessor.GetType(i).Name).AppendLine();
                }
            }

            stringBuilder
                .AppendLine("                }")
                .AppendLine("            }")
                .AppendLine()
                .AppendLine("            GeneratePropertyArray();")
                .AppendLine("            return true;")
                .Append("        }");

            return stringBuilder.ToString();
        }

        private static string GenerateDataTablePropertyArray(DataTableProcessor dataTableProcessor)
        {
            List<PropertyCollection> propertyCollections = new List<PropertyCollection>();
            for (int i = 0; i < dataTableProcessor.RawColumnCount; i++)
            {
                if (dataTableProcessor.IsCommentColumn(i))
                {
                    // 注释列
                    continue;
                }

                if (dataTableProcessor.IsIdColumn(i))
                {
                    // 编号列
                    continue;
                }

                string name = dataTableProcessor.GetName(i);
                if (!EndWithNumberRegex.IsMatch(name))
                {
                    continue;
                }

                string propertyCollectionName = EndWithNumberRegex.Replace(name, string.Empty);
                int id = int.Parse(EndWithNumberRegex.Match(name).Value);

                PropertyCollection propertyCollection = null;
                foreach (PropertyCollection pc in propertyCollections)
                {
                    if (pc.Name == propertyCollectionName)
                    {
                        propertyCollection = pc;
                        break;
                    }
                }

                if (propertyCollection == null)
                {
                    propertyCollection = new PropertyCollection(propertyCollectionName, dataTableProcessor.GetLanguageKeyword(i));
                    propertyCollections.Add(propertyCollection);
                }

                propertyCollection.AddItem(id, name);
            }

            StringBuilder stringBuilder = new StringBuilder();
            bool firstProperty = true;
            foreach (PropertyCollection propertyCollection in propertyCollections)
            {
                if (firstProperty)
                {
                    firstProperty = false;
                }
                else
                {
                    stringBuilder.AppendLine().AppendLine();
                }

                stringBuilder
                    .AppendFormat("        private KeyValuePair<int, {1}>[] m_{0} = null;", propertyCollection.Name, propertyCollection.LanguageKeyword).AppendLine()
                    .AppendLine()
                    .AppendFormat("        public int {0}Count", propertyCollection.Name).AppendLine()
                    .AppendLine("        {")
                    .AppendLine("            get")
                    .AppendLine("            {")
                    .AppendFormat("                return m_{0}.Length;", propertyCollection.Name).AppendLine()
                    .AppendLine("            }")
                    .AppendLine("        }")
                    .AppendLine()
                    .AppendFormat("        public {1} Get{0}(int id)", propertyCollection.Name, propertyCollection.LanguageKeyword).AppendLine()
                    .AppendLine("        {")
                    .AppendFormat("            foreach (KeyValuePair<int, {1}> i in m_{0})", propertyCollection.Name, propertyCollection.LanguageKeyword).AppendLine()
                    .AppendLine("            {")
                    .AppendLine("                if (i.Key == id)")
                    .AppendLine("                {")
                    .AppendLine("                    return i.Value;")
                    .AppendLine("                }")
                    .AppendLine("            }")
                    .AppendLine()
                    .AppendFormat("            throw new Exception(string.Format(\"Get{0} with invalid id '{{0}}'.\", id));", propertyCollection.Name).AppendLine()
                    .AppendLine("        }")
                    .AppendLine()
                    .AppendFormat("        public {1} Get{0}At(int index)", propertyCollection.Name, propertyCollection.LanguageKeyword).AppendLine()
                    .AppendLine("        {")
                    .AppendFormat("            if (index < 0 || index >= m_{0}.Length)", propertyCollection.Name).AppendLine()
                    .AppendLine("            {")
                    .AppendFormat("                throw new Exception(string.Format(\"Get{0}At with invalid index '{{0}}'.\", index));", propertyCollection.Name).AppendLine()
                    .AppendLine("            }")
                    .AppendLine()
                    .AppendFormat("            return m_{0}[index].Value;", propertyCollection.Name).AppendLine()
                    .Append("        }");
            }

            if (propertyCollections.Count > 0)
            {
                stringBuilder.AppendLine().AppendLine();
            }

            stringBuilder
                .AppendLine("        private void GeneratePropertyArray()")
                .AppendLine("        {");

            firstProperty = true;
            foreach (PropertyCollection propertyCollection in propertyCollections)
            {
                if (firstProperty)
                {
                    firstProperty = false;
                }
                else
                {
                    stringBuilder.AppendLine().AppendLine();
                }

                stringBuilder
                    .AppendFormat("            m_{0} = new KeyValuePair<int, {1}>[]", propertyCollection.Name, propertyCollection.LanguageKeyword).AppendLine()
                    .AppendLine("            {");

                int itemCount = propertyCollection.ItemCount;
                for (int i = 0; i < itemCount; i++)
                {
                    KeyValuePair<int, string> item = propertyCollection.GetItem(i);
                    stringBuilder.AppendFormat("                new KeyValuePair<int, {0}>({1}, {2}),", propertyCollection.LanguageKeyword, item.Key.ToString(), item.Value).AppendLine();
                }

                stringBuilder.Append("            };");
            }

            stringBuilder
                .AppendLine()
                .Append("        }");

            return stringBuilder.ToString();
        }

        private sealed class PropertyCollection
        {
            private readonly string m_Name;
            private readonly string m_LanguageKeyword;
            private readonly List<KeyValuePair<int, string>> m_Items;

            public PropertyCollection(string name, string languageKeyword)
            {
                m_Name = name;
                m_LanguageKeyword = languageKeyword;
                m_Items = new List<KeyValuePair<int, string>>();
            }

            public string Name
            {
                get
                {
                    return m_Name;
                }
            }

            public string LanguageKeyword
            {
                get
                {
                    return m_LanguageKeyword;
                }
            }

            public int ItemCount
            {
                get
                {
                    return m_Items.Count;
                }
            }

            public KeyValuePair<int, string> GetItem(int index)
            {
                if (index < 0 || index >= m_Items.Count)
                {
                    throw new Exception(string.Format("GetItem with invalid index '{0}'.", index));
                }

                return m_Items[index];
            }

            public void AddItem(int id, string propertyName)
            {
                m_Items.Add(new KeyValuePair<int, string>(id, propertyName));
            }
        }
    }
}
