using System.IO;

namespace DataTables.GeneratorCore;

public sealed partial class DataTableProcessor
{
    private sealed class SingleProcessor : GenericDataProcessor<float>
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
                return "float";
            }
        }

        public override string[] GetTypeStrings()
        {
            return new string[]
            {
                "float",
                "single",
                "system.single"
            };
        }

        public override float Parse(string value)
        {
            return string.IsNullOrEmpty(value) ? 0 : float.Parse(value);
        }

        public override void WriteToStream(BinaryWriter binaryWriter, string value)
        {
            binaryWriter.Write(Parse(value));
        }

        public override string GenerateDeserializeCode(GenerationContext context, string typeName, string propertyName, int depth)
        {
            return $"{propertyName} = reader.ReadSingle();";
        }
    }
}
