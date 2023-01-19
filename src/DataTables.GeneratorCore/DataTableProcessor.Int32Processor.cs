






using System.IO;
using DataTables.GeneratorCore;

namespace DataTables.GeneratorCore
{
    public sealed partial class DataTableProcessor
    {
        private sealed class Int32Processor : GenericDataProcessor<int>
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
                    return "int";
                }
            }

            public override string[] GetTypeStrings()
            {
                return new string[]
                {
                    "int",
                    "int32",
                    "system.int32"
                };
            }

            public override int Parse(string value)
            {
                return int.Parse(value);
            }

            public override void WriteToStream(BinaryWriter binaryWriter, string value)
            {
                binaryWriter.Write7BitEncodedInt32(Parse(value));
            }

            public override string GenerateDeserializeCode(GenerationContext context, string typeName, string propertyName, int depth)
            {
                return $"{propertyName} = reader.Read7BitEncodedInt32();";
            }
        }
    }
}
