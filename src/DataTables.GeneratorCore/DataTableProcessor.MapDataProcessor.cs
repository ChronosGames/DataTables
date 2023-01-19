using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace DataTables.GeneratorCore
{
    public sealed partial class DataTableProcessor
    {
        public class MapDataProcessor : DataProcessor
        {
            public override Type Type => throw new NotImplementedException();

            public override bool IsSystem => false;

            public override string LanguageKeyword => m_LanguageKeyword;

            private readonly string m_KeyTypeStr;
            private readonly string m_ValueTypeStr;
            private readonly string m_LanguageKeyword;
            private readonly string[] m_TypeStrings;

            public MapDataProcessor(DataProcessor keyProcessor, DataProcessor valueProcessor)
            {
                m_KeyTypeStr = keyProcessor.GetTypeStrings()[0];
                m_ValueTypeStr = valueProcessor.GetTypeStrings()[1];
                m_LanguageKeyword = $"Dictionary<{keyProcessor.LanguageKeyword}, {valueProcessor.LanguageKeyword}>";
                m_TypeStrings = new string[] { $"map<{m_KeyTypeStr},{m_ValueTypeStr}>" };
            }

            public override string[] GetTypeStrings()
            {
                return m_TypeStrings;
            }

            //public override Dictionary<T1, T2> Parse(string value)
            //{
            //    return JsonConvert.DeserializeObject<Dictionary<T1, T2>>(value);
            //}

            public override void WriteToStream(BinaryWriter binaryWriter, string value)
            {
                var abc = JsonConvert.DeserializeObject(value);


                binaryWriter.Write7BitEncodedInt32(arr.Count);
                foreach (var item in arr)
                {
                    DataProcessorUtility.GetDataProcessor(m_KeyTypeStr).WriteToStream(binaryWriter, JsonConvert.SerializeObject(item.Key));
                    DataProcessorUtility.GetDataProcessor(m_ValueTypeStr).WriteToStream(binaryWriter, JsonConvert.SerializeObject(item.Value));
                }
            }

            public override string GenerateDeserializeCode(GenerationContext context, string typeName, string propertyName, int depth)
            {
                return $"{propertyName} = new {LanguageKeyword}();\n"
                     + $"var mapCount{depth + 1} = reader.Read7BitEncodedInt32();\n"
                     + $"{Tabs(depth)}for (int x{depth + 1} = 0; x{depth + 1} < mapCount{depth + 1}; x{depth + 1}++)\n"
                     + $"{Tabs(depth)}{{\n"
                     + $"{Tabs(depth + 1)}{DataProcessorUtility.GetDataProcessor(m_KeyTypeStr).GenerateDeserializeCode(context, m_KeyTypeStr, $"var key{depth + 1}", depth + 1)}\n"
                     + $"{Tabs(depth + 1)}{DataProcessorUtility.GetDataProcessor(m_ValueTypeStr).GenerateDeserializeCode(context, m_ValueTypeStr, $"var [value{depth + 1}]", depth + 1)}\n"
                     + $"{Tabs(depth + 1)}{propertyName}.Add(key{depth + 1}, value{depth + 1});\n"
                     + $"{Tabs(depth)}}}";
            }
        }
    }
}
