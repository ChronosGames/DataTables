using System;
using System.Globalization;
using System.IO;

namespace DataTables.GeneratorCore;

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
            if (string.IsNullOrEmpty(value))
            {
                return 0;
            }

            var trimmed = value.Trim();
            // 兼容 JSON 字符串形式的数字，比如 "5"
            if (trimmed.Length > 0 && trimmed[0] == '"')
            {
                var s = JsonUtility.Deserialize<string>(trimmed) ?? string.Empty;
                if (int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
                {
                    return n;
                }
                // 回退：尝试直接 JSON 解析（若字符串本身是 JSON 数字）
            }

            return JsonUtility.Deserialize<int>(trimmed);
        }

        public override string GenerateTypeValue(string text) => Parse(text).ToString();

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
