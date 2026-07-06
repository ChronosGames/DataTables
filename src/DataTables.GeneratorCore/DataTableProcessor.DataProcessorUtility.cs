using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DataTables.GeneratorCore;

public sealed partial class DataTableProcessor
{
    private static class DataProcessorUtility
    {
        private static readonly ConcurrentDictionary<string, DataProcessor> s_DataProcessors = new ConcurrentDictionary<string, DataProcessor>(StringComparer.Ordinal);
        private static readonly List<string> s_SupportedTypeStrings = new();

        static DataProcessorUtility()
        {
            var dataProcessorBaseType = typeof(DataProcessor);
            var types = Assembly.GetExecutingAssembly().GetTypes();
            foreach (var type in types)
            {
                if (!type.IsClass || type.IsAbstract)
                {
                    continue;
                }

                if (dataProcessorBaseType.IsAssignableFrom(type))
                {
                    DataProcessor dataProcessor = (DataProcessor)Activator.CreateInstance(type)!;
                    if (!dataProcessor.IsSystem)
                    {
                        continue;
                    }

                    foreach (string typeString in dataProcessor.GetTypeStrings())
                    {
                        var key = NormalizeCacheKey(typeString);
                        s_DataProcessors.TryAdd(key, dataProcessor);
                        if (!s_SupportedTypeStrings.Contains(key, StringComparer.Ordinal))
                        {
                            s_SupportedTypeStrings.Add(key);
                        }
                    }
                }
            }

            s_SupportedTypeStrings.AddRange(["array<T>", "map<K,V>", "json<T>", "enum<T>", "custom<T>"]);
        }

        public static DataProcessor GetDataProcessor(string type)
        {
            var descriptor = DataTypeParser.Parse(type, GetSupportedTypeStrings());
            if (s_DataProcessors.TryGetValue(descriptor.NormalizedSignature, out var dataProcessor))
            {
                return dataProcessor;
            }

            var name = descriptor.Name.ToLowerInvariant();
            DataProcessor created = name switch
            {
                "enum" => new EnumProcessor(descriptor.Arguments[0].Name),
                "array" => new ArrayDataProcessor(GetDataProcessor(descriptor.Arguments[0].NormalizedSignature)),
                "map" => new MapDataProcessor(GetDataProcessor(descriptor.Arguments[0].NormalizedSignature), GetDataProcessor(descriptor.Arguments[1].NormalizedSignature)),
                "json" => new JSONProcessor(descriptor.Arguments[0].Name),
                "custom" => new CustomizeProcessor(descriptor.Arguments[0].Name),
                _ => throw new DataTypeParseException(type ?? string.Empty, 0, "当前支持的类型之一", GetSupportedTypeStrings()),
            };

            foreach (var ts in created.GetTypeStrings())
            {
                s_DataProcessors.TryAdd(NormalizeCacheKey(ts), created);
            }
            s_DataProcessors.TryAdd(descriptor.NormalizedSignature, created);
            return created;
        }

        public static IReadOnlyList<string> GetSupportedTypeStrings()
        {
            return s_SupportedTypeStrings.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        }

        private static string NormalizeCacheKey(string type)
        {
            try
            {
                return DataTypeParser.NormalizeSignature(type, Array.Empty<string>());
            }
            catch (DataTypeParseException)
            {
                return (type ?? string.Empty).Trim().ToLowerInvariant();
            }
        }
    }
}
