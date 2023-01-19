﻿






using System.IO;

namespace DataTables.GeneratorCore
{
    public sealed partial class DataTableProcessor
    {
        private sealed class DoubleProcessor : GenericDataProcessor<double>
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
                    return "double";
                }
            }

            public override string[] GetTypeStrings()
            {
                return new string[]
                {
                    "double",
                    "system.double"
                };
            }

            public override double Parse(string value)
            {
                return double.Parse(value);
            }

            public override void WriteToStream(BinaryWriter binaryWriter, string value)
            {
                binaryWriter.Write(Parse(value));
            }

            public override string GenerateDeserializeCode(GenerationContext context, string typeName, string propertyName, int depth)
            {
                return $"{propertyName} = reader.ReadDouble();";
            }
        }
    }
}
