using System;
using System.IO;

namespace DataTables.GeneratorCore;

public sealed partial class DataTableProcessor
{
    private sealed class DateTimeProcessor : GenericDataProcessor<DateTime>
    {
        public override bool IsSystem => true;

        public override string LanguageKeyword
        {
            get
            {
                return "DateTime";
            }
        }

        public override string[] GetTypeStrings()
        {
            return new string[]
            {
                "datetime",
                "system.datetime"
            };
        }

        public override Type Type => typeof(DateTime);

        public override DateTime Parse(string value)
        {
            return DateTime.Parse(value);
        }

        public override string GenerateDeserializeCode(GenerationContext context, string typeName, string propertyName, int depth)
        {
            return $"{propertyName} = new DateTime(reader.ReadInt64());";
        }

        public override void WriteToStream(BinaryWriter binaryWriter, string value)
        {
            binaryWriter.Write(Parse(value).Ticks);
        }
    }
}
