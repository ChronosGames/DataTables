using System;
using System.Collections.Generic;
using System.Linq;

namespace DataTables.GeneratorCore;

public sealed partial class DataTableProcessor
{
    public sealed class DataTypeDescriptor
    {
        public DataTypeDescriptor(string originalText, string normalizedSignature, string name, IReadOnlyList<DataTypeDescriptor> arguments)
        {
            OriginalText = originalText;
            NormalizedSignature = normalizedSignature;
            Name = name;
            Arguments = arguments;
        }

        public string OriginalText { get; }
        public string NormalizedSignature { get; }
        public string Name { get; }
        public IReadOnlyList<DataTypeDescriptor> Arguments { get; }
    }

    public sealed class DataTypeParseException : FormatException
    {
        public DataTypeParseException(string originalType, int position, string expectedFormat, IReadOnlyList<string> supportedTypes)
            : base(BuildMessage(originalType, position, expectedFormat, supportedTypes))
        {
            OriginalType = originalType;
            Position = position;
            ExpectedFormat = expectedFormat;
            SupportedTypes = supportedTypes;
        }

        public string OriginalType { get; }
        public int Position { get; }
        public string ExpectedFormat { get; }
        public IReadOnlyList<string> SupportedTypes { get; }

        private static string BuildMessage(string originalType, int position, string expectedFormat, IReadOnlyList<string> supportedTypes)
        {
            return $"类型解析失败: OriginalType='{originalType}', Position={position}, ExpectedFormat='{expectedFormat}', SupportedTypes=[{string.Join(", ", supportedTypes)}]";
        }
    }

    private static class DataTypeParser
    {
        private static readonly Dictionary<string, string> s_Aliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["int32"] = "int",
            ["system.int32"] = "int",
            ["single"] = "float",
            ["system.single"] = "float",
            ["datetime"] = "DateTime",
            ["system.datetime"] = "DateTime",
            ["a"] = "array",
            ["m"] = "map",
            ["e"] = "enum",
        };

        public static DataTypeDescriptor Parse(string? type, IReadOnlyList<string> supportedTypes)
        {
            var original = type ?? string.Empty;
            var parser = new Parser(original, supportedTypes);
            return parser.Parse();
        }

        public static string NormalizeSignature(string? type, IReadOnlyList<string> supportedTypes) => Parse(type, supportedTypes).NormalizedSignature;

        private sealed class Parser
        {
            private readonly string m_Text;
            private readonly IReadOnlyList<string> m_SupportedTypes;
            private int m_Position;

            public Parser(string text, IReadOnlyList<string> supportedTypes)
            {
                m_Text = text;
                m_SupportedTypes = supportedTypes;
            }

            public DataTypeDescriptor Parse()
            {
                SkipWhitespace();
                var result = ParseType();
                SkipWhitespace();
                if (m_Position != m_Text.Length)
                {
                    Fail("类型结尾，或泛型格式 array<T>、map<K,V>、json<T>、enum<T>");
                }
                return result;
            }

            private DataTypeDescriptor ParseType()
            {
                SkipWhitespace();
                var start = m_Position;
                var name = ParseName();
                if (name.Length == 0)
                {
                    Fail("类型名称");
                }

                var canonicalName = NormalizeName(name);
                SkipWhitespace();
                if (m_Position < m_Text.Length && m_Text[m_Position] == '<')
                {
                    m_Position++;
                    var args = new List<DataTypeDescriptor>();
                    while (true)
                    {
                        args.Add(ParseType());
                        SkipWhitespace();
                        if (m_Position < m_Text.Length && m_Text[m_Position] == ',')
                        {
                            m_Position++;
                            continue;
                        }
                        if (m_Position < m_Text.Length && m_Text[m_Position] == '>')
                        {
                            m_Position++;
                            break;
                        }
                        Fail(args.Count == 1 ? "',' 或 '>'" : "'>'");
                    }

                    ValidateGeneric(canonicalName, args.Count, start);
                    var signature = $"{canonicalName.ToLowerInvariant()}<{string.Join(",", args.Select(a => a.NormalizedSignature))}>";
                    return new DataTypeDescriptor(m_Text, signature, canonicalName, args);
                }

                var normalized = IsSupportedLeaf(canonicalName) ? canonicalName.ToLowerInvariant() : canonicalName;
                return new DataTypeDescriptor(m_Text, normalized, canonicalName, Array.Empty<DataTypeDescriptor>());
            }

            private bool IsSupportedLeaf(string name)
            {
                return m_SupportedTypes.Any(type => type.IndexOf('<') < 0 && type.Equals(name, StringComparison.OrdinalIgnoreCase));
            }

            private string ParseName()
            {
                var start = m_Position;
                while (m_Position < m_Text.Length)
                {
                    var c = m_Text[m_Position];
                    if (char.IsLetterOrDigit(c) || c == '_' || c == '.') m_Position++;
                    else break;
                }
                return m_Text[start..m_Position];
            }

            private string NormalizeName(string name) => s_Aliases.TryGetValue(name, out var alias) ? alias : name;

            private void ValidateGeneric(string name, int argumentCount, int position)
            {
                var lower = name.ToLowerInvariant();
                var ok = lower switch
                {
                    "array" => argumentCount == 1,
                    "json" => argumentCount == 1,
                    "enum" => argumentCount == 1,
                    "custom" => argumentCount == 1,
                    "map" => argumentCount == 2,
                    _ => false,
                };
                if (!ok)
                {
                    m_Position = position;
                    Fail("array<T>、map<K,V>、json<T>、enum<T> 或 custom<T>");
                }
            }

            private void SkipWhitespace()
            {
                while (m_Position < m_Text.Length && char.IsWhiteSpace(m_Text[m_Position])) m_Position++;
            }

            private void Fail(string expected) => throw new DataTypeParseException(m_Text, m_Position, expected, m_SupportedTypes);
        }
    }
}
