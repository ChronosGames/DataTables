






using System.IO;

namespace DataTables.GeneratorCore
{
    public sealed partial class DataTableProcessor
    {
        public abstract class DataProcessor
        {
            public abstract System.Type Type
            {
                get;
            }

            public abstract bool IsSystem
            {
                get;
            }

            public abstract string LanguageKeyword
            {
                get;
            }

            protected string Tabs(int depth)
            {
                return new string(' ', 4 * (depth + 5));
            }

            public abstract string[] GetTypeStrings();

            public abstract string GenerateDeserializeCode(GenerationContext context, string typeName, string propertyName, int depth);

            public abstract void WriteToStream(BinaryWriter binaryWriter, string value);
        }
    }
}
