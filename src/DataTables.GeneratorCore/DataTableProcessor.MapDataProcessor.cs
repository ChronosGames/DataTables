using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace DataTables.GeneratorCore;

public sealed partial class DataTableProcessor
{
    public class MapDataProcessor : DataProcessor
    {
        public override Type Type => typeof(Hashtable);

        public override bool IsSystem => false;

        public override string LanguageKeyword => m_LanguageKeyword;

        private readonly string m_KeyTypeStr;
        private readonly string m_ValueTypeStr;
        private readonly string m_LanguageKeyword;

        public MapDataProcessor()
        {
            m_KeyTypeStr = string.Empty;
            m_ValueTypeStr = string.Empty;
            m_LanguageKeyword = string.Empty;
        }

        public MapDataProcessor(DataProcessor keyProcessor, DataProcessor valueProcessor)
        {
            m_KeyTypeStr = keyProcessor.GetTypeStrings()[0];
            m_ValueTypeStr = valueProcessor.GetTypeStrings()[0];
            m_LanguageKeyword = $"Dictionary<{keyProcessor.LanguageKeyword}, {valueProcessor.LanguageKeyword}>";
        }

        public override string[] GetTypeStrings()
        {
            return
            [
                $"map<{m_KeyTypeStr},{m_ValueTypeStr}>",
                $"M<{m_KeyTypeStr},{m_ValueTypeStr}>"
            ];
        }

        public override string GenerateTypeValue(string text) => throw new NotSupportedException();

        public override void WriteToStream(BinaryWriter binaryWriter, string value)
        {
            // 空的单元格时，自动补齐为空字母
            if (string.IsNullOrEmpty(value))
            {
                value = "{}";
            }

            var dict = JsonUtility.Deserialize<Hashtable>(value)!;
            var keyProcessor = DataProcessorUtility.GetDataProcessor(m_KeyTypeStr);
            var valueProcessor = DataProcessorUtility.GetDataProcessor(m_ValueTypeStr);
            var entries = new List<(string KeyText, string ValueText)>(dict.Count);
            foreach (DictionaryEntry item in dict)
            {
                var keyText = JsonUtility.Serialize(item.Key);
                entries.Add((keyText, JsonUtility.Serialize(item.Value)));
            }

            entries.Sort(static (left, right) =>
            {
                var compare = StringComparer.Ordinal.Compare(left.KeyText, right.KeyText);
                return compare != 0 ? compare : StringComparer.Ordinal.Compare(left.ValueText, right.ValueText);
            });

            binaryWriter.Write7BitEncodedInt32(dict.Count);
            foreach (var entry in entries)
            {
                keyProcessor.WriteToStream(binaryWriter, entry.KeyText);
                valueProcessor.WriteToStream(binaryWriter, entry.ValueText);
            }
        }

        public override string GenerateDeserializeCode(GenerationContext context, string typeName, string propertyName, int depth)
        {
            return "{\n"
                 + $"{Tabs(depth + 1)}{propertyName} = new {LanguageKeyword}();\n"
                 + $"{Tabs(depth + 1)}var __{propertyName}_Count{depth + 1} = reader.Read7BitEncodedInt32();\n"
                 + $"{Tabs(depth + 1)}for (int x{depth + 1} = 0; x{depth + 1} < __{propertyName}_Count{depth + 1}; x{depth + 1}++)\n"
                 + $"{Tabs(depth + 1)}{{\n"
                 + $"{Tabs(depth + 2)}{DataProcessorUtility.GetDataProcessor(m_KeyTypeStr).LanguageKeyword} key{depth + 1};\n"
                 + $"{Tabs(depth + 2)}{DataProcessorUtility.GetDataProcessor(m_KeyTypeStr).GenerateDeserializeCode(context, m_KeyTypeStr, $"key{depth + 1}", depth + 2)}\n"
                 + $"{Tabs(depth + 2)}{DataProcessorUtility.GetDataProcessor(m_ValueTypeStr).LanguageKeyword} value{depth + 1};\n"
                 + $"{Tabs(depth + 2)}{DataProcessorUtility.GetDataProcessor(m_ValueTypeStr).GenerateDeserializeCode(context, m_ValueTypeStr, $"value{depth + 1}", depth + 2)}\n"
                 + $"{Tabs(depth + 2)}{propertyName}.Add(key{depth + 1}, value{depth + 1});\n"
                 + $"{Tabs(depth + 1)}}}\n"
                 + $"{Tabs(depth)}}}";
        }
    }
}
