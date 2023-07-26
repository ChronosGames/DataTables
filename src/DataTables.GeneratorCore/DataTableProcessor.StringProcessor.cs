using System.IO;

namespace DataTables.GeneratorCore;

public sealed partial class DataTableProcessor
{
    private sealed class StringProcessor : GenericDataProcessor<string>
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
                return "string";
            }
        }

        public override string[] GetTypeStrings()
        {
            return new string[]
            {
                "string",
                "system.string"
            };
        }

        public override string Parse(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value;
        }

        public override string GenerateTypeValue(string text) => $"{Parse(text)}";

        public override void WriteToStream(BinaryWriter binaryWriter, string value)
        {
            binaryWriter.Write(Parse(value));
        }

        public override string GenerateDeserializeCode(GenerationContext context, string typeName, string propertyName, int depth)
        {
            return $"{propertyName} = reader.ReadString();";
        }
    }
}
