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

            public override string GenerateDeserializeCode(GenerationContext context, Property property)
            {
                return $"Enum.TryParse(reader.ReadString(), out {GetPropertyTypeString(property)} __{property.Name}); {property.Name} = __{property.Name};";
            }

            internal string GetPropertyTypeString(Property property)
            {
                if (property.TypeName.StartsWith("Enum"))
                {
                    return property.TypeName.Substring(4);
                }

                return property.TypeName;
            }
        }
    }
}
