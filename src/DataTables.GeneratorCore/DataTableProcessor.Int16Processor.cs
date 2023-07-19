using System.IO;

namespace DataTables.GeneratorCore;

public sealed partial class DataTableProcessor
{
    private sealed class Int16Processor : GenericDataProcessor<short>
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
                return "short";
            }
        }

        public override string[] GetTypeStrings()
        {
            return new string[]
            {
                "short",
                "int16",
                "system.int16"
            };
        }

        public override short Parse(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return 0;
            }

            return JsonUtility.Deserialize<short>(value);
        }

        public override void WriteToStream(BinaryWriter binaryWriter, string value)
        {
            binaryWriter.Write(Parse(value));
        }

        public override string GenerateDeserializeCode(GenerationContext context, string typeName, string propertyName, int depth)
        {
            return $"{propertyName} = reader.ReadInt16();";
        }
    }
}
