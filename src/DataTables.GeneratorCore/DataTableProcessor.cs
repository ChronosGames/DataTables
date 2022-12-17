using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DataTables.GeneratorCore
{
    public sealed partial class DataTableProcessor
    {
        private const string CommentLineSeparator = "#";
        private static readonly char[] DataSplitSeparators = new char[] { '\t' };
        private static readonly char[] DataTrimSeparators = new char[] { '\"' };

        private readonly string[] m_NameRow;
        private readonly string[] m_TypeRow;
        private readonly string[] m_DefaultValueRow;
        private readonly string[] m_CommentRow;
        private readonly int m_ContentStartRow;
        private readonly int m_IdColumn;

        private readonly DataProcessor[] m_DataProcessor;
        private readonly string[][] m_RawValues;
        private readonly string[] m_Strings;

        private string m_CodeTemplate;
        private DataTableCodeGenerator m_CodeGenerator;

        public DataTableProcessor(string dataTableFileName, Encoding encoding, int nameRow, int typeRow, int? defaultValueRow, int? commentRow, int contentStartRow, int idColumn)
        {
            if (string.IsNullOrEmpty(dataTableFileName))
            {
                throw new Exception("Data table file name is invalid.");
            }

            if (!dataTableFileName.EndsWith(".txt", StringComparison.Ordinal))
            {
                throw new Exception(string.Format("Data table file '{0}' is not a txt.", dataTableFileName));
            }

            if (!File.Exists(dataTableFileName))
            {
                throw new Exception(string.Format("Data table file '{0}' is not exist.", dataTableFileName));
            }

            string[] lines = File.ReadAllLines(dataTableFileName, encoding);
            int rawRowCount = lines.Length;

            int rawColumnCount = 0;
            List<string[]> rawValues = new List<string[]>();
            for (int i = 0; i < lines.Length; i++)
            {
                string[] rawValue = lines[i].Split(DataSplitSeparators);
                for (int j = 0; j < rawValue.Length; j++)
                {
                    rawValue[j] = rawValue[j].Trim(DataTrimSeparators);
                }

                if (i == 0)
                {
                    rawColumnCount = rawValue.Length;
                }
                else if (rawValue.Length != rawColumnCount)
                {
                    throw new Exception(string.Format("Data table file '{0}', raw Column is '{2}', but line '{1}' column is '{3}'.", dataTableFileName, i, rawColumnCount, rawValue.Length));
                }

                rawValues.Add(rawValue);
            }

            m_RawValues = rawValues.ToArray();

            if (nameRow < 0)
            {
                throw new Exception(string.Format("Name row '{0}' is invalid.", nameRow));
            }

            if (typeRow < 0)
            {
                throw new Exception(string.Format("Type row '{0}' is invalid.", typeRow));
            }

            if (contentStartRow < 0)
            {
                throw new Exception(string.Format("Content start row '{0}' is invalid.", contentStartRow));
            }

            if (idColumn < 0)
            {
                throw new Exception(string.Format("Id column '{0}' is invalid.", idColumn));
            }

            if (nameRow >= rawRowCount)
            {
                throw new Exception(string.Format("Name row '{0}' >= raw row count '{1}' is not allow.", nameRow, rawRowCount));
            }

            if (typeRow >= rawRowCount)
            {
                throw new Exception(string.Format("Type row '{0}' >= raw row count '{1}' is not allow.", typeRow, rawRowCount));
            }

            if (defaultValueRow.HasValue && defaultValueRow.Value >= rawRowCount)
            {
                throw new Exception(string.Format("Default value row '{0}' >= raw row count '{1}' is not allow.", defaultValueRow.Value, rawRowCount));
            }

            if (commentRow.HasValue && commentRow.Value >= rawRowCount)
            {
                throw new Exception(string.Format("Comment row '{0}' >= raw row count '{1}' is not allow.", commentRow.Value, rawRowCount));
            }

            if (contentStartRow > rawRowCount)
            {
                throw new Exception(string.Format("Content start row '{0}' > raw row count '{1}' is not allow.", contentStartRow, rawRowCount));
            }

            if (idColumn >= rawColumnCount)
            {
                throw new Exception(string.Format("Id column '{0}' >= raw column count '{1}' is not allow.", idColumn, rawColumnCount));
            }

            m_NameRow = m_RawValues[nameRow];
            m_TypeRow = m_RawValues[typeRow];
            m_DefaultValueRow = defaultValueRow.HasValue ? m_RawValues[defaultValueRow.Value] : null;
            m_CommentRow = commentRow.HasValue ? m_RawValues[commentRow.Value] : null;
            m_ContentStartRow = contentStartRow;
            m_IdColumn = idColumn;

            m_DataProcessor = new DataProcessor[rawColumnCount];
            for (int i = 0; i < rawColumnCount; i++)
            {
                if (i == IdColumn)
                {
                    m_DataProcessor[i] = DataProcessorUtility.GetDataProcessor("id");
                }
                else
                {
                    m_DataProcessor[i] = DataProcessorUtility.GetDataProcessor(m_TypeRow[i]);
                }
            }

            Dictionary<string, int> strings = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = contentStartRow; i < rawRowCount; i++)
            {
                if (IsCommentRow(i))
                {
                    continue;
                }

                for (int j = 0; j < rawColumnCount; j++)
                {
                    if (m_DataProcessor[j].LanguageKeyword != "string")
                    {
                        continue;
                    }

                    string str = m_RawValues[i][j];
                    if (strings.ContainsKey(str))
                    {
                        strings[str]++;
                    }
                    else
                    {
                        strings[str] = 1;
                    }
                }
            }

            m_Strings = strings.OrderBy(value => value.Key).OrderByDescending(value => value.Value).Select(value => value.Key).ToArray();

            m_CodeTemplate = null;
            m_CodeGenerator = null;
        }

        public int RawRowCount
        {
            get
            {
                return m_RawValues.Length;
            }
        }

        public int RawColumnCount
        {
            get
            {
                return m_RawValues.Length > 0 ? m_RawValues[0].Length : 0;
            }
        }

        public int StringCount
        {
            get
            {
                return m_Strings.Length;
            }
        }

        public int ContentStartRow
        {
            get
            {
                return m_ContentStartRow;
            }
        }

        public int IdColumn
        {
            get
            {
                return m_IdColumn;
            }
        }

        public bool IsCommentRow(int rawRow)
        {
            if (rawRow < 0 || rawRow >= RawRowCount)
            {
                throw new Exception(string.Format("Raw row '{0}' is out of range.", rawRow));
            }

            return GetValue(rawRow, 0).StartsWith(CommentLineSeparator, StringComparison.Ordinal);
        }

        public string GetName(int rawColumn)
        {
            if (rawColumn < 0 || rawColumn >= RawColumnCount)
            {
                throw new Exception(string.Format("Raw column '{0}' is out of range.", rawColumn));
            }

            return m_NameRow[rawColumn];
        }

        public bool IsSystem(int rawColumn)
        {
            if (rawColumn < 0 || rawColumn >= RawColumnCount)
            {
                throw new Exception(string.Format("Raw column '{0}' is out of range.", rawColumn));
            }

            return m_DataProcessor[rawColumn].IsSystem;
        }

        public System.Type GetType(int rawColumn)
        {
            if (rawColumn < 0 || rawColumn >= RawColumnCount)
            {
                throw new Exception(string.Format("Raw column '{0}' is out of range.", rawColumn));
            }

            return m_DataProcessor[rawColumn].Type;
        }

        public string GetLanguageKeyword(int rawColumn)
        {
            if (rawColumn < 0 || rawColumn >= RawColumnCount)
            {
                throw new Exception(string.Format("Raw column '{0}' is out of range.", rawColumn));
            }

            return m_DataProcessor[rawColumn].LanguageKeyword;
        }

        public string GetDefaultValue(int rawColumn)
        {
            if (rawColumn < 0 || rawColumn >= RawColumnCount)
            {
                throw new Exception(string.Format("Raw column '{0}' is out of range.", rawColumn));
            }

            return m_DefaultValueRow != null ? m_DefaultValueRow[rawColumn] : null;
        }

        public string GetComment(int rawColumn)
        {
            if (rawColumn < 0 || rawColumn >= RawColumnCount)
            {
                throw new Exception(string.Format("Raw column '{0}' is out of range.", rawColumn));
            }

            return m_CommentRow != null ? m_CommentRow[rawColumn] : null;
        }

        public string GetValue(int rawRow, int rawColumn)
        {
            if (rawRow < 0 || rawRow >= RawRowCount)
            {
                throw new Exception(string.Format("Raw row '{0}' is out of range.", rawRow));
            }

            if (rawColumn < 0 || rawColumn >= RawColumnCount)
            {
                throw new Exception(string.Format("Raw column '{0}' is out of range.", rawColumn));
            }

            return m_RawValues[rawRow][rawColumn];
        }

        public string GetString(int index)
        {
            if (index < 0 || index >= StringCount)
            {
                throw new Exception(string.Format("String index '{0}' is out of range.", index));
            }

            return m_Strings[index];
        }

        public int GetStringIndex(string str)
        {
            for (int i = 0; i < StringCount; i++)
            {
                if (m_Strings[i] == str)
                {
                    return i;
                }
            }

            return -1;
        }

        public static bool GenerateDataFile(GenerationContext context, string outputFileName, Action<string> logger)
        {
            try
            {
                using (FileStream fileStream = new FileStream(outputFileName, FileMode.Create, FileAccess.Write))
                {
                    using (BinaryWriter binaryWriter = new BinaryWriter(fileStream, Encoding.UTF8))
                    {
                        for (int i = 0; i < context.RowCount; i++)
                        {
                            byte[] bytes = GetRowBytes(context, i);
                            binaryWriter.Write7BitEncodedInt32(bytes.Length);
                            binaryWriter.Write(bytes);
                        }
                    }
                }

                logger(string.Format("Parse data table '{0}' success.", outputFileName));
                return true;
            }
            catch (Exception exception)
            {
                logger(string.Format("Parse data table '{0}' failure, exception is '{1}'.", outputFileName, exception));
                return false;
            }
        }

        public bool GenerateCodeFile(string outputFileName, Encoding encoding, object userData = null)
        {
            if (string.IsNullOrEmpty(m_CodeTemplate))
            {
                throw new Exception("You must set code template first.");
            }

            if (string.IsNullOrEmpty(outputFileName))
            {
                throw new Exception("Output file name is invalid.");
            }

            try
            {
                StringBuilder stringBuilder = new StringBuilder(m_CodeTemplate);
                if (m_CodeGenerator != null)
                {
                    m_CodeGenerator(this, stringBuilder, userData);
                }

                using (FileStream fileStream = new FileStream(outputFileName, FileMode.Create, FileAccess.Write))
                {
                    using (StreamWriter stream = new StreamWriter(fileStream, encoding))
                    {
                        stream.Write(stringBuilder.ToString());
                    }
                }

                //Debug.Log(string.Format("Generate code file '{0}' success.", outputFileName));
                return true;
            }
            catch (Exception exception)
            {
                //Debug.LogError(string.Format("Generate code file '{0}' failure, exception is '{1}'.", outputFileName, exception));
                return false;
            }
        }

        private static byte[] GetRowBytes(GenerationContext context, int rawRow)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream, Encoding.UTF8))
                {
                    for (int rawColumn = 0; rawColumn < context.ColumnCount; rawColumn++)
                    {
                        var processor = DataProcessorUtility.GetDataProcessor(context.Properties[rawColumn].TypeName);
                        processor.WriteToStream(binaryWriter, context.Cells[rawRow, rawColumn].ToString().Trim());
                    }

                    return memoryStream.ToArray();
                }
            }
        }

        public static string GetDeserializeMethodString(GenerationContext context, Property property)
        {
            var processor = DataProcessorUtility.GetDataProcessor(property.TypeName);
            return processor.GenerateDeserializeCode(context, property);
        }
    }
}
