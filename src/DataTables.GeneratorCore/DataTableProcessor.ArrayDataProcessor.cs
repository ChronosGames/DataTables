using System;
using System.Collections;
using System.IO;

namespace DataTables.GeneratorCore;

public sealed partial class DataTableProcessor
{
    public class ArrayDataProcessor : DataProcessor
    {
        public override Type Type => typeof(ArrayList);

        public override bool IsSystem => false;

        public override string LanguageKeyword => m_LanguageKeyword;

        private readonly string m_KeyTypeStr;
        private readonly string m_LanguageKeyword;

        public ArrayDataProcessor()
        {
            m_KeyTypeStr = string.Empty;
            m_LanguageKeyword = string.Empty;
        }

        public ArrayDataProcessor(DataProcessor keyProcessor)
        {
            m_KeyTypeStr = keyProcessor.GetTypeStrings()[0];
            m_LanguageKeyword = $"{keyProcessor.LanguageKeyword}[]";
        }

        public override string[] GetTypeStrings()
        {
            return new string[]
            {
                $"array<{m_KeyTypeStr}>",
                $"A<{m_KeyTypeStr}>",
            };
        }

        public override void WriteToStream(BinaryWriter binaryWriter, string value)
        {
            // 默认为空时自动补齐为空数组
            if (string.IsNullOrEmpty(value) || value == "0")
            {
                value = "[]";
            }

            var arr = JsonUtility.Deserialize<ArrayList>(value);
            if (arr == null)
            {
                binaryWriter.Write7BitEncodedInt32(0);
                return;
            }

            binaryWriter.Write7BitEncodedInt32(arr.Count);
            foreach (var item in arr)
            {
                DataProcessorUtility.GetDataProcessor(m_KeyTypeStr).WriteToStream(binaryWriter, JsonUtility.Serialize(item));
            }
        }

        public override string GenerateDeserializeCode(GenerationContext context, string typeName, string propertyName, int depth)
        {
            return "{\n"
                 + $"{Tabs(depth + 1)}var __{propertyName}_Count{depth + 1} = reader.Read7BitEncodedInt32();\n"
                 + $"{Tabs(depth + 1)}{propertyName} = new {BuildArrayInitString(LanguageKeyword, $"__{propertyName}_Count{depth + 1}")};\n"
                 + $"{Tabs(depth + 1)}for (int x{depth + 1} = 0; x{depth + 1} < __{propertyName}_Count{depth + 1}; x{depth + 1}++)\n"
                 + $"{Tabs(depth + 1)}{{\n"
                 + $"{Tabs(depth + 2)}{DataProcessorUtility.GetDataProcessor(m_KeyTypeStr).LanguageKeyword} key{depth + 1};\n"
                 + $"{Tabs(depth + 2)}{DataProcessorUtility.GetDataProcessor(m_KeyTypeStr).GenerateDeserializeCode(context, Type.Name, $"key{depth + 1}", depth + 2)}\n"
                 + $"{Tabs(depth + 2)}{propertyName}[x{depth + 1}] = key{depth + 1};\n"
                 + $"{Tabs(depth + 1)}}}\n"
                 + $"{Tabs(depth)}}}";
        }

        /// <summary>
        /// 获取数组后缀
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        private int GetIndexOfFirstArrayLabelPairSuffix(string text)
        {
            int index = -1;

            for (int i = text.Length - 2; i >= 0; i -= 2)
            {
                if (text[i] == '[' && text[i + 1] == ']')
                {
                    index = i;
                    continue;
                }

                break;
            }

            return index;
        }

        private string BuildArrayInitString(string text, string propertyName)
        {
            int index = GetIndexOfFirstArrayLabelPairSuffix(text);
            return text.Substring(0, index + 1) + propertyName + text.Substring(index + 1);
        }
    }
}
