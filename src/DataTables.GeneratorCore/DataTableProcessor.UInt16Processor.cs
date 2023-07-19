using System.IO;

namespace DataTables.GeneratorCore;

public sealed partial class DataTableProcessor
{
    private sealed class UInt16Processor : GenericDataProcessor<ushort>
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
                return "ushort";
            }
        }

        public override string[] GetTypeStrings()
        {
            return new string[]
            {
                "ushort",
                "uint16",
                "system.uint16"
            };
        }

        public override ushort Parse(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return 0;
            }

            return JsonUtility.Deserialize<ushort>(value);
        }

        public override void WriteToStream(BinaryWriter binaryWriter, string value)
        {
            binaryWriter.Write(Parse(value));
        }

        public override string GenerateDeserializeCode(GenerationContext context, string typeName, string propertyName, int depth)
        {
            return $"{propertyName} = reader.ReadUInt16();";
        }
    }
}
