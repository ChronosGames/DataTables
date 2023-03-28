






using System;
using System.IO;
using Newtonsoft.Json;

namespace DataTables.GeneratorCore
{
    public sealed partial class DataTableProcessor
    {
        private sealed class UInt64Processor : GenericDataProcessor<ulong>
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
                    return "ulong";
                }
            }

            public override string[] GetTypeStrings()
            {
                return new string[]
                {
                    "ulong",
                    "uint64",
                    "system.uint64"
                };
            }

            public override ulong Parse(string value)
            {
                if (string.IsNullOrEmpty(value))
                {
                    return 0;
                }

                object v = JsonConvert.DeserializeObject(value);
                return Convert.ToUInt64(v);
            }

            public override void WriteToStream(BinaryWriter binaryWriter, string value)
            {
                binaryWriter.Write7BitEncodedUInt64(Parse(value));
            }

            public override string GenerateDeserializeCode(GenerationContext context, string typeName, string propertyName, int depth)
            {
                return $"{propertyName} = reader.Read7BitEncodedUInt64();";
            }
        }
    }
}
