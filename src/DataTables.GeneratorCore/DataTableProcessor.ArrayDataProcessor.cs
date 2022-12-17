using System.IO;
using Newtonsoft.Json;

namespace DataTables.GeneratorCore
{
    public sealed partial class DataTableProcessor
    {
        public abstract class ArrayDataProcessor<T> : GenericDataProcessor<T[]>
        {
            public override System.Type Type
            {
                get
                {
                    return typeof(T);
                }
            }

            public override bool IsSystem => false;

            public override string LanguageKeyword => "array";

            public override string[] GetTypeStrings()
            {
                return new string[] { "array" };
            }

            public override T[] Parse(string value)
            {
                return JsonConvert.DeserializeObject<T[]>(value);
            }

            public override void WriteToStream(BinaryWriter binaryWriter, string value)
            {
                var arr = Parse(value);
                binaryWriter.Write7BitEncodedInt32(arr.Length);
                foreach (var item in arr)
                {
                    DataProcessorUtility.GetDataProcessor(typeof(T).Name).WriteToStream(binaryWriter, JsonConvert.SerializeObject(item));
                }
            }

            public override string GenerateDeserializeCode(GenerationContext context, string typeName, string propertyName, int depth)
            {
                return $"{propertyName} = new {typeof(T).Name}[reader.Read7BitEncodedInt32()];\n"
                     + $"{Tabs(depth)}for (int x{depth + 1} = 0; x{depth + 1} < {propertyName}.Length; x{depth + 1}++)\n"
                     + $"{Tabs(depth)}{{\n"
                     + $"{Tabs(depth + 1)}{DataProcessorUtility.GetDataProcessor(typeof(T).Name).GenerateDeserializeCode(context, typeof(T).Name, propertyName + $"[x{depth + 1}]", depth + 1)}\n"
                     + $"{Tabs(depth)}}}";
            }
        }

        public class ArrayIntProcessor : ArrayDataProcessor<int>
        {
            public override string[] GetTypeStrings()
            {
                return new string[] { "int[]" };
            }
        }

        public class ArrayStringProcessor : ArrayDataProcessor<string>
        {
            public override string[] GetTypeStrings()
            {
                return new string[] { "string[]" };
            }
        }

        public class CharsProcessor : ArrayDataProcessor<char>
        {
            public override string[] GetTypeStrings()
            {
                return new string[] { "char[]" };
            }

            public override string GenerateDeserializeCode(GenerationContext context, string typeName, string propertyName, int depth)
            {
                return $"var __{propertyName}_Count = reader.Read7BitEncodedInt(); {propertyName} = reader.ReadChars(__{propertyName}_Count);";
            }

            public override void WriteToStream(BinaryWriter binaryWriter, string value)
            {
                var arr = Parse(value);
                binaryWriter.Write7BitEncodedInt32(arr.Length);
                binaryWriter.Write(arr);
            }
        }
    }
}
