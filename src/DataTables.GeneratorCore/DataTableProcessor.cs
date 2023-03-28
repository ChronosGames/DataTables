using System;
using System.IO;
using System.Text;

namespace DataTables.GeneratorCore
{
    public sealed partial class DataTableProcessor
    {
        public static bool GenerateDataFile(GenerationContext context, string outputFileName, Action<string> logger)
        {
            try
            {
                using (FileStream fileStream = new FileStream(outputFileName, FileMode.Create, FileAccess.Write))
                {
                    using (BinaryWriter binaryWriter = new BinaryWriter(fileStream, Encoding.UTF8))
                    {
                        for (int i = 0; i < context.RowCount; i++)
                        {
                            byte[] bytes = GetRowBytes(context, i);
                            binaryWriter.Write7BitEncodedInt32(bytes.Length);
                            binaryWriter.Write(bytes);
                        }
                    }
                }

                logger(string.Format("# Generate {0}.bytes to: {1}.", context.RealClassName, outputFileName));
                return true;
            }
            catch (Exception exception)
            {
                logger(string.Format("# Generate {0}.bytes failure, exception is '{1}'.", context.RealClassName, exception));
                return false;
            }
        }

        private static byte[] GetRowBytes(GenerationContext context, int rawRow)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream, Encoding.UTF8))
                {
                    for (int rawColumn = 0; rawColumn < context.ColumnCount; rawColumn++)
                    {
                        var processor = DataProcessorUtility.GetDataProcessor(context.Properties[rawColumn].TypeName);

                        try
                        {
                            processor.WriteToStream(binaryWriter, context.Cells[rawRow, rawColumn].ToString().Trim());
                        }
                        catch (Exception e)
                        {
                            throw new Exception($"解析单元格内容时出错: Row={rawRow}, Col={rawColumn}", e);
                        }
                    }

                    return memoryStream.ToArray();
                }
            }
        }

        public static string GetDeserializeMethodString(GenerationContext context, Property property)
        {
            var processor = DataProcessorUtility.GetDataProcessor(property.TypeName);
            return processor.GenerateDeserializeCode(context, processor.Type.Name, property.Name, 0);
        }

        public static string GetLanguageKeyword(Property property)
        {
            var processor = DataProcessorUtility.GetDataProcessor(property.TypeName);
            return processor.LanguageKeyword;
        }
    }
}
