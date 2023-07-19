using System;
using System.IO;

namespace DataTables.GeneratorCore;

public sealed partial class DataTableProcessor
{
    private sealed class ByteProcessor : GenericDataProcessor<byte>
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
                return "byte";
            }
        }

        public override string[] GetTypeStrings()
        {
            return new string[]
            {
                "byte",
                "system.byte"
            };
        }

        public override Type Type => typeof(byte);

        public override byte Parse(string value)
        {
            return string.IsNullOrEmpty(value) ? default : byte.Parse(value);
        }

        public override void WriteToStream(BinaryWriter binaryWriter, string value)
        {
            binaryWriter.Write(Parse(value));
        }

        public override string GenerateDeserializeCode(GenerationContext context, string typeName, string propertyName, int depth)
        {
            return $"{propertyName} = reader.ReadByte();";
        }
    }
}
