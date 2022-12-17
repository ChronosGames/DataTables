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
        }

        public class ArrayIntProcessor : ArrayDataProcessor<int>
        {
            public override string[] GetTypeStrings()
            {
                return new string[] { "int[]" };
            }

            public override string GenerateDeserializeCode(GenerationContext context, Property property)
            {
                return $"{property.Name} = new int[reader.Read7BitEncodedInt()]; for (int i = 0; i < {property.Name}.Length; i++) {{ {property.Name}[i] = reader.ReadInt32(); }}";
            }
        }

        public class ArrayStringProcessor : ArrayDataProcessor<string>
        {
            public override string[] GetTypeStrings()
            {
                return new string[] { "string[]" };
            }

            public override string GenerateDeserializeCode(GenerationContext context, Property property)
            {
                return $"{property.Name} = new string[reader.Read7BitEncodedInt()]; for (int i = 0; i < {property.Name}.Length; i++) {{ {property.Name}[i] = reader.ReadString(); }}";
            }
        }

        public class CharsProcessor : ArrayDataProcessor<char>
        {
            public override string[] GetTypeStrings()
            {
                return new string[] { "char[]" };
            }

            public override string GenerateDeserializeCode(GenerationContext context, Property property)
            {
                return $"var __{property.Name}_Count = reader.Read7BitEncodedInt(); {property.Name} = reader.ReadChars(__{property.Name}_Count);";
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
