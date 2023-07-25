using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace DataTables.GeneratorCore;

public sealed partial class DataTableProcessor
{
    private static class DataProcessorUtility
    {
        private static readonly ConcurrentDictionary<string, DataProcessor> s_DataProcessors = new ConcurrentDictionary<string, DataProcessor>(StringComparer.Ordinal);

        static DataProcessorUtility()
        {
            System.Type dataProcessorBaseType = typeof(DataProcessor);
            Assembly assembly = Assembly.GetExecutingAssembly();
            System.Type[] types = assembly.GetTypes();
            for (int i = 0; i < types.Length; i++)
            {
                if (!types[i].IsClass || types[i].IsAbstract)
                {
                    continue;
                }

                if (dataProcessorBaseType.IsAssignableFrom(types[i]))
                {
                    DataProcessor dataProcessor = (DataProcessor)Activator.CreateInstance(types[i])!;
                    if (!dataProcessor.IsSystem)
                    {
                        continue;
                    }

                    foreach (string typeString in dataProcessor.GetTypeStrings())
                    {
                        s_DataProcessors.TryAdd(typeString.ToLowerInvariant(), dataProcessor);
                    }
                }
            }
        }

        public static DataProcessor GetDataProcessor(string type)
        {
            if (type == null)
            {
                type = string.Empty;
            }

            DataProcessor? dataProcessor;
            if (s_DataProcessors.TryGetValue(type.ToLowerInvariant(), out dataProcessor))
            {
                return dataProcessor;
            }
            else if (type.StartsWith("enum", StringComparison.InvariantCultureIgnoreCase))
            {
                var str1 = FindGenericString(type);
                if (s_DataProcessors.TryGetValue($"enum<{str1}>", out var abc))
                {
                    return abc;
                }
                else
                {
                    abc = new EnumProcessor(str1);
                    foreach (var ts in abc.GetTypeStrings())
                    {
                        s_DataProcessors.TryAdd(ts, abc);
                    }
                    return abc;
                }
            }
            else if (type.StartsWith("array", StringComparison.InvariantCultureIgnoreCase))
            {
                var str1 = FindGenericString(type);

                if (s_DataProcessors.TryGetValue($"array<{str1}>", out var abc))
                {
                    return abc;
                }
                else
                {
                    var keyProcessor = GetDataProcessor(str1);

                    abc = new ArrayDataProcessor(keyProcessor);
                    foreach (var ts in abc.GetTypeStrings())
                    {
                        if (s_DataProcessors.ContainsKey(ts))
                        {
                            continue;
                        }

                        s_DataProcessors.TryAdd(ts, abc);
                    }
                    return abc;
                }
            }
            else if (type.StartsWith("map", StringComparison.InvariantCultureIgnoreCase))
            {
                var str1 = FindGenericString(type);

                var index = FindSplitCharIndex(str1);
                if (index == -1)
                {
                    throw new Exception(string.Format("Not supported data processor type '{0}'.", type));
                }

                var keyTypeStr = str1.Substring(0, index).Trim();
                var valueTypeStr = str1.Substring(index + 1).Trim();

                if (s_DataProcessors.TryGetValue($"map<{keyTypeStr},{valueTypeStr}>", out var abc))
                {
                    return abc;
                }
                else
                {
                    var keyProcessor = GetDataProcessor(keyTypeStr);
                    var valueProcessor = GetDataProcessor(valueTypeStr);

                    abc = new MapDataProcessor(keyProcessor, valueProcessor);
                    foreach (var ts in abc.GetTypeStrings())
                    {
                        s_DataProcessors.TryAdd(ts, abc);
                    }
                    return abc;
                }
            }
            else if (type.StartsWith("json", StringComparison.InvariantCultureIgnoreCase))
            {
                var str1 = FindGenericString(type);

                if (s_DataProcessors.TryGetValue($"json<{str1}>", out var abc))
                {
                    return abc;
                }
                else
                {
                    abc = new JSONProcessor(str1);
                    foreach (var ts in abc.GetTypeStrings())
                    {
                        s_DataProcessors.TryAdd(ts, abc);
                    }
                    return abc;
                }
            }
            else if (type.StartsWith("custom", StringComparison.InvariantCultureIgnoreCase))
            {
                var str1 = FindGenericString(type);

                if (s_DataProcessors.TryGetValue($"custom<{str1}>", out var abc))
                {
                    return abc;
                }
                else
                {
                    abc = new CustomProcessor(str1);
                    foreach (var ts in abc.GetTypeStrings())
                    {
                        s_DataProcessors.TryAdd(ts, abc);
                    }
                    return abc;
                }
            }

            throw new Exception(string.Format("Not supported data processor type '{0}'.", type));
        }

        public static string FindGenericString(string type)
        {
            return type.Substring(type.IndexOf('<') + 1, type.LastIndexOf('>') - type.IndexOf('<') - 1).Trim();
        }

        private static int FindSplitCharIndex(string str1)
        {
            var index = -1;
            var depth = 0;
            for (int i = 0; i < str1.Length; i++)
            {
                if (str1[i] == '<')
                {
                    depth++;
                    continue;
                }
                else if (str1[i] == '>')
                {
                    depth--;
                    continue;
                }
                else if (str1[i] == ',' && depth == 0)
                {
                    index = i;
                    break;
                }
            }

            return index;
        }
    }
}
