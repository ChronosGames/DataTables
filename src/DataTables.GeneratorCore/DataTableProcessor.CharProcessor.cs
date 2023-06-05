using System;
using System.IO;

namespace DataTables.GeneratorCore;

public sealed partial class DataTableProcessor
{
    private sealed class CharProcessor : GenericDataProcessor<char>
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
                return "char";
            }
        }

        public override string[] GetTypeStrings()
        {
            return new string[]
            {
                "char",
                "system.char"
            };
        }

        public override Type Type => typeof(char);

        public override char Parse(string value)
        {
            return string.IsNullOrEmpty(value) ? default : char.Parse(value);
        }

        public override void WriteToStream(BinaryWriter binaryWriter, string value)
        {
            binaryWriter.Write(Parse(value));
        }

        public override string GenerateDeserializeCode(GenerationContext context, string typeName, string propertyName, int depth)
        {
            return $"{propertyName} = reader.ReadChar();";
        }
    }
}
