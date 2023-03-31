using System;
using System.IO;
using Newtonsoft.Json;

namespace DataTables.GeneratorCore
{
    public sealed partial class DataTableProcessor
    {
        private sealed class JSONProcessor : GenericDataProcessor<string>
        {
            public override bool IsSystem
            {
                get
                {
                    return false;
                }
            }

            public override string LanguageKeyword => m_TypeString;

            public override string[] GetTypeStrings()
            {
                return new string[]
                {
                    $"json<{m_TypeString}>",
                };
            }

            public override Type Type => typeof(string);

            private readonly string m_TypeString;

            public JSONProcessor() { }

            public JSONProcessor(string typeString)
            {
                m_TypeString = typeString;
            }

            public override string Parse(string value)
            {
                return value.StartsWith("\"") ? JsonConvert.DeserializeObject<string>(value) : value;
            }

            public override void WriteToStream(BinaryWriter binaryWriter, string value)
            {
                binaryWriter.Write(Parse(value));
            }

            public override string GenerateDeserializeCode(GenerationContext context, string typeName, string propertyName, int depth)
            {
                return $"{{\n"
                    + $"{Tabs(depth + 1)}var __jsonStr = reader.ReadString();\n"
                    + $"{Tabs(depth + 1)}{propertyName} = Utility.Json.ToObject<{m_TypeString}>(__jsonStr);\n"
                    + $"{Tabs(depth)}}}";
            }
        }
    }
}
