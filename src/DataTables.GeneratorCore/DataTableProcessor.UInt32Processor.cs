using System;
using System.Globalization;
using System.IO;

namespace DataTables.GeneratorCore;

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
            if (string.IsNullOrEmpty(value))
            {
                return 0;
            }

            var trimmed = value.Trim();
            if (trimmed.Length > 0 && trimmed[0] == '"')
            {
                var s = JsonUtility.Deserialize<string>(trimmed) ?? string.Empty;
                if (uint.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
                {
                    return n;
                }
            }

            return JsonUtility.Deserialize<uint>(trimmed);
        }

        public override string GenerateTypeValue(string text) => Parse(text).ToString();

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
