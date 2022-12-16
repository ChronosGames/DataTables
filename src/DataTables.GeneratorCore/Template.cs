using System;
using System.Collections.Generic;
using System.Text;

namespace DataTables.GeneratorCore
{
    public partial class DatabaseBuilderTemplate
    {
        public string Namespace { get; set; }
        public string Using { get; set; }
        public string PrefixClassName { get; set; }
        public GenerationContext[] GenerationContexts { get; set; }

        public string ClassName => PrefixClassName + "DatabaseBuilder";
    }

    public partial class MemoryDatabaseTemplate
    {
        public string Namespace { get; set; }
        public string Using { get; set; }
        public string PrefixClassName { get; set; }
        public GenerationContext[] GenerationContexts { get; set; }
        public string ClassName => PrefixClassName + "MemoryDatabase";
    }

    public partial class MetaMemoryDatabaseTemplate
    {
        public string Namespace { get; set; }
        public string Using { get; set; }
        public string PrefixClassName { get; set; }
        public GenerationContext[] GenerationContexts { get; set; }
        public string ClassName => PrefixClassName + "MetaMemoryDatabase";
    }

    public partial class ImmutableBuilderTemplate
    {
        public string Namespace { get; set; }
        public string Using { get; set; }
        public string PrefixClassName { get; set; }
        public GenerationContext[] GenerationContexts { get; set; }
        public string ClassName => PrefixClassName + "ImmutableBuilder";
    }

    public partial class MessagePackResolverTemplate
    {
        public string Namespace { get; set; }
        public string Using { get; set; }
        public string PrefixClassName { get; set; }
        public GenerationContext[] GenerationContexts { get; set; }
        public string ClassName => PrefixClassName + "MasterMemoryResolver";
    }

    public partial class TableTemplate
    {
        public string Namespace { get; set; }
        public string Using { get; set; }
        public string PrefixClassName { get; set; }
        public GenerationContext GenerationContext { get; set; }

        public bool ThrowKeyIfNotFound { get; set; }
    }

    public partial class CodeTemplate
    {
        public string Using { get; set; }

        public GenerationContext GenerationContext { get; set; }

        public string Namespace => GenerationContext.Namespace;

        public string ClassName => GenerationContext.PrefixClassName + GenerationContext.ClassName;

        internal string GetPropertyTypeString(Property property)
        {
            if (property.Type.StartsWith("Enum"))
            {
                return property.Type.Substring(4);
            }

            return property.Type;
        }

        internal string GetDeserializeMethodString(Property property)
        {
            switch (property.Type)
            {
                case "short":
                    return $"{property.Name} = reader.ReadInt16();";
                case "ushort":
                    return $"{property.Name} = reader.ReadUInt16();";
                case "int":
                    return $"{property.Name} = reader.ReadInt32();";
                case "uint":
                    return $"{property.Name} = reader.ReadUInt32();";
                case "long":
                    return $"{property.Name} = reader.ReadInt64();";
                case "ulong":
                    return $"{property.Name} = reader.ReadUInt64();";
                case "float":
                    return $"{property.Name} = reader.ReadSingle();";
                case "double":
                    return $"{property.Name} = reader.ReadDouble();";
                case "bool":
                    return $"{property.Name} = reader.ReadBoolean();";
                case "char":
                    return $"{property.Name} = reader.ReadChar();";
                case "char[]":
                    return $"var __{property.Name}_Count = reader.Read7BitEncodedInt(); {property.Name} = reader.ReadChars(__{property.Name}_Count);";
                case "int[]":
                    return $"{property.Name} = new int[reader.Read7BitEncodedInt()]; for (int i = 0; i < {property.Name}.Length; i++) {{ {property.Name}[i] = reader.ReadInt32(); }}";
                case "string[]":
                    return $"{property.Name} = new string[reader.Read7BitEncodedInt()]; for (int i = 0; i < {property.Name}.Length; i++) {{ {property.Name}[i] = reader.ReadString(); }}";
                default:
                {
                    if (property.Type.StartsWith("Enum"))
                    {
                        return $"Enum.TryParse(reader.ReadString(), out {GetPropertyTypeString(property)} __{property.Name}); {property.Name} = __{property.Name};";
                    }

                    throw new NotImplementedException($"Unknown Type: {property.Type}");
                }
            }
        }
    }

    public partial class DataTemplate
    {

    }
}
