using System;
using System.Collections.Generic;
using System.IO;

namespace DataTables.GeneratorCore;

public sealed partial class DataTableProcessor
{
    private static Dictionary<string, bool> kBoolMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "1", true },
        { "0", false },
        { "y", true },
        { "n", false },
        { "yes", true },
        { "no", false },
        { "true", true },
        { "false", false },
        { "true()", true },
        { "false()", false },
    };

    private sealed class BooleanProcessor : GenericDataProcessor<bool>
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
                return "bool";
            }
        }

        public override Type Type => typeof(bool);

        public override string[] GetTypeStrings()
        {
            return new string[]
            {
                "bool",
                "boolean",
                "system.boolean"
            };
        }

        public override string GenerateTypeValue(string text)
        {
            return Parse(text) ? "true" : "false";
        }

        public override bool Parse(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            if (kBoolMap.TryGetValue(value, out var result))
            {
                return result;
            }

            throw new InvalidCastException($"无法将{value}转换为bool类型");
        }

        public override string GenerateDeserializeCode(GenerationContext context, string typeName, string propertyName, int depth)
        {
            return $"{propertyName} = reader.ReadBoolean();";
        }

        public override void WriteToStream(BinaryWriter binaryWriter, string value)
        {
            binaryWriter.Write(Parse(value));
        }
    }
}
