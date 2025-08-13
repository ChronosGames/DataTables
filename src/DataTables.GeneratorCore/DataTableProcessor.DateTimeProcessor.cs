using System;
using System.IO;
using System.Globalization;

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
			if (string.IsNullOrWhiteSpace(value))
			{
				throw new InvalidCastException("无法将空值转换为DateTime类型");
			}

			var formats = new[] { "yyyy-MM-dd", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss" };
			var text = value.Trim();
			if (DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
			{
				return result;
			}

			throw new InvalidCastException($"无法将{value}转换为DateTime类型，期望格式：yyyy-MM-dd 或 yyyy-MM-dd HH:mm:ss 或 yyyy-MM-ddTHH:mm:ss（InvariantCulture）");
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
