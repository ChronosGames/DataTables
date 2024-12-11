using System;
using System.IO;

namespace DataTables.GeneratorCore;

public sealed partial class DataTableProcessor
{
    private sealed class CustomizeProcessor : GenericDataProcessor<string>
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
            return
            [
                $"custom<{m_TypeString}>"
            ];
        }

        public override Type Type => typeof(string);

        private readonly string m_TypeString;

        // ReSharper disable once UnusedMember.Local
        public CustomizeProcessor() : this(string.Empty)
        { }

        public CustomizeProcessor(string typeString)
        {
            m_TypeString = typeString;
        }

        public override string GenerateTypeValue(string text) => $"new {m_TypeString}(@\"{text}\")";

        public override string Parse(string value)
        {
            return value.StartsWith("\"") ? JsonUtility.Deserialize<string>(value)! : value;
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
