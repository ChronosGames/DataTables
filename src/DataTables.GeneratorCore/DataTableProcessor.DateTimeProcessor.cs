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

        public override string GenerateTypeValue(string text)
        {
            var datetime = Parse(text);
            return $"new DateTime({datetime.Year}, {datetime.Month}, {datetime.Day}, {datetime.Hour}, {datetime.Minute}, {datetime.Second})";
        }

        public override DateTime Parse(string value)
        {
            new DateTime(2012, 2, 2, 3, 4, 5);

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
