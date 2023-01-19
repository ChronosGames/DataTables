






using System.IO;

namespace DataTables.GeneratorCore
{
    public sealed partial class DataTableProcessor
    {
        private sealed class UInt32Processor : GenericDataProcessor<uint>
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
                    return "uint";
                }
            }

            public override string[] GetTypeStrings()
            {
                return new string[]
                {
                    "uint",
                    "uint32",
                    "system.uint32"
                };
            }

            public override uint Parse(string value)
            {
                return uint.Parse(value);
            }

            public override void WriteToStream(BinaryWriter binaryWriter, string value)
            {
                binaryWriter.Write7BitEncodedUInt32(Parse(value));
            }

            public override string GenerateDeserializeCode(GenerationContext context, string typeName, string propertyName, int depth)
            {
                return $"{propertyName} = reader.Read7BitEncodedUInt32();";
            }
        }
    }
}
