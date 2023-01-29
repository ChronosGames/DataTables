﻿using System;
using System.Collections;
using System.IO;
using Newtonsoft.Json;

namespace DataTables.GeneratorCore
{
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
            private readonly string[] m_TypeStrings;

            public MapDataProcessor()
            {
            }

            public MapDataProcessor(DataProcessor keyProcessor, DataProcessor valueProcessor)
            {
                m_KeyTypeStr = keyProcessor.GetTypeStrings()[0];
                m_ValueTypeStr = valueProcessor.GetTypeStrings()[0];
                m_LanguageKeyword = $"Dictionary<{keyProcessor.LanguageKeyword}, {valueProcessor.LanguageKeyword}>";
                m_TypeStrings = new string[] { $"map<{m_KeyTypeStr},{m_ValueTypeStr}>" };
            }

            public override string[] GetTypeStrings()
            {
                return m_TypeStrings;
            }

            public override void WriteToStream(BinaryWriter binaryWriter, string value)
            {
                var dict = JsonConvert.DeserializeObject<Hashtable>(value);

                binaryWriter.Write7BitEncodedInt32(dict.Count);
                foreach (DictionaryEntry item in dict)
                {
                    DataProcessorUtility.GetDataProcessor(m_KeyTypeStr).WriteToStream(binaryWriter, JsonConvert.SerializeObject(item.Key));
                    DataProcessorUtility.GetDataProcessor(m_ValueTypeStr).WriteToStream(binaryWriter, JsonConvert.SerializeObject(item.Value));
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
}
