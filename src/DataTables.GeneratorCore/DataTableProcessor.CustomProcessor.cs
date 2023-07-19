using System;
using System.IO;

namespace DataTables.GeneratorCore;

public sealed partial class DataTableProcessor
{
    private sealed class CustomProcessor : GenericDataProcessor<string>
    {
        public override bool IsSystem
        {
            get
            {
                return false;
            }
        }

        public override string LanguageKeyword => m_TypeString;

        public override string[] GetTypeStrings()
        {
            return new string[]
            {
                $"custom<{m_TypeString}>",
            };
        }

        public override Type Type => typeof(string);

        private readonly string m_TypeString;

        public CustomProcessor()
        {
            m_TypeString = string.Empty;
        }

        public CustomProcessor(string typeString)
        {
            m_TypeString = typeString;
        }

        public override string Parse(string value)
        {
            return value.StartsWith("\"") ? JsonUtility.Deserialize<string>(value) : value;
        }

        public override void WriteToStream(BinaryWriter binaryWriter, string value)
        {
            binaryWriter.Write(Parse(value));
        }

        public override string GenerateDeserializeCode(GenerationContext context, string typeName, string propertyName, int depth)
        {
            return $"{propertyName} = new {m_TypeString}(reader.ReadString());";
        }
    }
}
