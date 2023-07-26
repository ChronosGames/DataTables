using System.IO;

namespace DataTables.GeneratorCore;

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
            return new string(' ', 4 * (depth + 2));
        }

        public abstract string[] GetTypeStrings();

        /// <summary>
        /// 将字符串文本转化为实际变量值
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public abstract string GenerateTypeValue(string text);

        public abstract string GenerateDeserializeCode(GenerationContext context, string typeName, string propertyName, int depth);

        public abstract void WriteToStream(BinaryWriter binaryWriter, string value);
    }
}
