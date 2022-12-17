using System;
using System.IO;

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
                    return true;
                }
            }

            public override string LanguageKeyword
            {
                get
                {
                    return "enum";
                }
            }

            public override string[] GetTypeStrings()
            {
                return new string[]
                {
                    "enum",
                    "system.enum"
                };
            }

            public override string Parse(string value)
            {
                return value;
            }

            public override void WriteToStream(BinaryWriter binaryWriter, string value)
            {
                binaryWriter.Write(Parse(value));
            }

            public override string GenerateDeserializeCode(GenerationContext context, string typeName, string propertyName, int depth)
            {
                return $"if (Enum.TryParse(reader.ReadString(), out {GetPropertyTypeString(typeName)} __{propertyName}))\n                    {{\n                        {propertyName} = __{propertyName};\n                    }}";
            }

            internal string GetPropertyTypeString(string typeName)
            {
                if (typeName.StartsWith("Enum"))
                {
                    return typeName.Substring(4);
                }

                return typeName;
            }
        }
    }
}
