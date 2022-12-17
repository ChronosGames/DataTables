//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using System.IO;

namespace DataTables.GeneratorCore
{
    public sealed partial class DataTableProcessor
    {
        private sealed class Int64Processor : GenericDataProcessor<long>
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
                    return "long";
                }
            }

            public override string[] GetTypeStrings()
            {
                return new string[]
                {
                    "long",
                    "int64",
                    "system.int64"
                };
            }

            public override long Parse(string value)
            {
                return long.Parse(value);
            }

            public override void WriteToStream(BinaryWriter binaryWriter, string value)
            {
                binaryWriter.Write7BitEncodedInt64(Parse(value));
            }

            public override string GenerateDeserializeCode(GenerationContext context, Property property)
            {
                return $"{property.Name} = reader.Read7BitEncodedInt64();";
            }
        }
    }
}
