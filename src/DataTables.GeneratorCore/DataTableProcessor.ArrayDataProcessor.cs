using System;
using System.IO;
using System.Collections;
using Newtonsoft.Json;

namespace DataTables.GeneratorCore
{
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
                var arr = JsonConvert.DeserializeObject<ArrayList>(value);

                binaryWriter.Write7BitEncodedInt32(arr.Count);
                foreach (var item in arr)
                {
                    DataProcessorUtility.GetDataProcessor(m_KeyTypeStr).WriteToStream(binaryWriter, JsonConvert.SerializeObject(item));
                }
            }

            public override string GenerateDeserializeCode(GenerationContext context, string typeName, string propertyName, int depth)
            {
                return "{\n"
                     + $"{Tabs(depth + 1)}var __{propertyName}_Count{depth + 1} = reader.Read7BitEncodedInt32();\n"
                     + $"{Tabs(depth + 1)}{propertyName} = new {LanguageKeyword.Substring(0, LanguageKeyword.Length - 2)}[__{propertyName}_Count{depth + 1}];\n"
                     + $"{Tabs(depth + 1)}for (int x{depth + 1} = 0; x{depth + 1} < __{propertyName}_Count{depth + 1}; x{depth + 1}++)\n"
                     + $"{Tabs(depth + 1)}{{\n"
                     + $"{Tabs(depth + 2)}{DataProcessorUtility.GetDataProcessor(m_KeyTypeStr).LanguageKeyword} key{depth + 1};\n"
                     + $"{Tabs(depth + 2)}{DataProcessorUtility.GetDataProcessor(m_KeyTypeStr).GenerateDeserializeCode(context, Type.Name, $"key{depth + 1}", depth + 2)}\n"
                     + $"{Tabs(depth + 2)}{propertyName}[x{depth + 1}] = key{depth + 1};\n"
                     + $"{Tabs(depth + 1)}}}\n"
                     + $"{Tabs(depth)}}}";
            }
        }
    }
}
