using System;
using System.IO;
using System.Globalization;

namespace DataTables.GeneratorCore;

public sealed partial class DataTableProcessor
{
    private sealed class DecimalProcessor : GenericDataProcessor<decimal>
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
                return "decimal";
            }
        }

        public override string[] GetTypeStrings()
        {
            return new string[]
            {
                "decimal",
                "system.decimal"
            };
        }

        public override Type Type => typeof(decimal);

        public override string GenerateTypeValue(string text) => Parse(text).ToString() + 'm';

		public override decimal Parse(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				throw new InvalidCastException("无法将空值转换为decimal类型");
			}
			return decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);
		}

        public override void WriteToStream(BinaryWriter binaryWriter, string value)
        {
            binaryWriter.Write(Parse(value));
        }

        public override string GenerateDeserializeCode(GenerationContext context, string typeName, string propertyName, int depth)
        {
            return $"{propertyName} = reader.ReadDecimal();";
        }
    }
}
