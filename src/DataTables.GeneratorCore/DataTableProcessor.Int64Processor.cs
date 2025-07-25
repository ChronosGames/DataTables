using System.Buffers.Binary;
using System.IO;

namespace DataTables.GeneratorCore;

public sealed partial class DataTableProcessor
{
    private sealed class Int64Processor : GenericDataProcessor<long>
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
                return "long";
            }
        }

        public override string[] GetTypeStrings()
        {
            return
            [
                "long",
                "int64",
                "system.int64"
            ];
        }

        public override long Parse(string value)
        {
            return string.IsNullOrEmpty(value) ? 0 : JsonUtility.Deserialize<long>(value);
        }

        public override string GenerateTypeValue(string text) => Parse(text).ToString() + 'L';

        public override void WriteToStream(BinaryWriter binaryWriter, string value)
        {
            binaryWriter.Write7BitEncodedInt64(Parse(value));
        }

        public override string GenerateDeserializeCode(GenerationContext context, string typeName, string propertyName, int depth)
        {
            return $"{propertyName} = reader.Read7BitEncodedInt64();";
        }
    }
}
