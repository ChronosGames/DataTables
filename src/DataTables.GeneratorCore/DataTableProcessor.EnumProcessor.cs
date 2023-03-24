using System;
using System.IO;
using Newtonsoft.Json;

namespace DataTables.GeneratorCore
{
    public sealed partial class DataTableProcessor
    {
        private sealed class EnumProcessor : GenericDataProcessor<string>
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
                    $"enum<{m_TypeString}>",
                    $"E<{m_TypeString}>",
                };
            }

            public override Type Type => typeof(string);

            private readonly string m_TypeString;

            public EnumProcessor() { }

            public EnumProcessor(string typeString)
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
                    + $"{Tabs(depth + 1)}{m_TypeString} __enumVal = default;\n"
                    + $"{Tabs(depth + 1)}var __enumStr = reader.ReadString();\n"
                    + $"{Tabs(depth + 1)}if (!string.IsNullOrEmpty(__enumStr) && !Enum.TryParse(__enumStr, out __enumVal))\n"
                    + $"{Tabs(depth + 1)}{{\n"
                    + $"{Tabs(depth + 2)}throw new ArgumentException();\n"
                    + $"{Tabs(depth + 1)}}}\n"
                    + $"{Tabs(depth + 1)}{propertyName} = __enumVal;\n"
                    + $"{Tabs(depth)}}}";
            }
        }
    }
}
