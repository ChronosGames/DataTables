using System;
using System.Linq;

namespace DataTables.GeneratorCore;

public sealed class TableSchemaValidator : ITableSchemaValidator
{
    private readonly GenerationContext m_Context;
    private readonly ParseOptions m_Options;

    public TableSchemaValidator(GenerationContext context, ParseOptions options)
    {
        m_Context = context;
        m_Options = options;
    }

    public bool Validate(int firstDataRowIndex)
    {
        if (string.IsNullOrEmpty(m_Context.ClassName)) return false;
        if (m_Context.Fields.All(x => x.IsIgnore)) return false;
        if (firstDataRowIndex == -1) throw new Exception("表格头部信息不全");

        var fieldMap = m_Context.Fields
            .Where(f => !string.IsNullOrEmpty(f.Name))
            .GroupBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        NormalizeIndexes(fieldMap);
        NormalizeGroups(fieldMap);
        ValidateIndexDeclarations();
        m_Context.RefreshIndexDefinitionsFromLegacyLists();
        return true;
    }

    private void NormalizeIndexes(System.Collections.Generic.Dictionary<string, XField> fieldMap)
    {
        for (int idx = 0; idx < m_Context.Indexs.Count; idx++)
        {
            var index = m_Context.Indexs[idx];
            for (int i = 0; i < index.Length; i++)
            {
                var rawName = (index[i] ?? string.Empty).Trim();
                if (!fieldMap.TryGetValue(rawName, out var field))
                {
                    var ignoredField = m_Context.Fields.FirstOrDefault(f =>
                        string.Equals(f.Name, rawName, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrEmpty(f.Title) && string.Equals(f.Title, rawName, StringComparison.OrdinalIgnoreCase)));

                    if (ignoredField != null && ignoredField.IsIgnore)
                    {
                        var reason = ignoredField.IsTagFiltered ? $"因标签过滤被忽略（当前过滤标签: {m_Options.FilterColumnTags}）" :
                                   ignoredField.IsComment ? "为注释列被忽略" : "被忽略";
                        throw new Exception($"Index配置引用了被忽略的字段: {rawName} ({reason})。请调整标签过滤参数或修改Index配置。");
                    }

                    var available = string.Join(", ", m_Context.Fields.Where(f => !string.IsNullOrEmpty(f.Name)).Select(f => f.Name));
                    throw new Exception($"Index配置中发现不存在的字段: {rawName}. 可用字段: [{available}]");
                }
                if (field.IsIgnore)
                {
                    var reason = field.IsTagFiltered ? $"因标签过滤被忽略（当前过滤标签: {m_Options.FilterColumnTags}）" : field.IsComment ? "为注释列被忽略" : "被忽略";
                    throw new Exception($"Index配置引用了被忽略的字段: {rawName} ({reason})。请调整标签过滤参数或修改Index配置。");
                }
                ValidateDictionaryKeyField(field, "Index");
                if (!string.Equals(index[i], field.Name, StringComparison.Ordinal)) index[i] = field.Name;
            }
        }
    }

    private void NormalizeGroups(System.Collections.Generic.Dictionary<string, XField> fieldMap)
    {
        for (int g = 0; g < m_Context.Groups.Count; g++)
        {
            var group = m_Context.Groups[g];
            for (int i = 0; i < group.Length; i++)
            {
                var rawName = (group[i] ?? string.Empty).Trim();
                if (!fieldMap.TryGetValue(rawName, out var field))
                {
                    var available = string.Join(", ", m_Context.Fields.Where(f => !string.IsNullOrEmpty(f.Name)).Select(f => f.Name));
                    throw new Exception($"Group配置中发现不存在的字段: {rawName}. 可用字段: [{available}]");
                }
                if (field.IsIgnore)
                {
                    var reason = field.IsTagFiltered ? "(因标签过滤被忽略)" : field.IsComment ? "(为注释列被忽略)" : "(被忽略)";
                    throw new Exception($"Group配置引用了被忽略的字段: {rawName} {reason}");
                }
                ValidateDictionaryKeyField(field, "Group");
                if (!string.Equals(group[i], field.Name, StringComparison.Ordinal)) group[i] = field.Name;
            }
        }
    }

    private void ValidateIndexDeclarations()
    {
        var uniqueKeys = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        var orderedSignatures = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var index in m_Context.Indexs)
        {
            ValidateFieldList(index, "Index");
            var key = string.Join("\u001F", index);
            if (!uniqueKeys.Add(key))
            {
                throw new Exception($"重复的唯一索引声明: {string.Join("&", index)}");
            }

            ValidateStableOrder(index, orderedSignatures, "Index");
        }

        foreach (var group in m_Context.Groups)
        {
            ValidateFieldList(group, "Group");
            ValidateStableOrder(group, orderedSignatures, "Group");
        }
    }

    private static void ValidateFieldList(string[] fields, string kind)
    {
        if (fields.Length == 0)
        {
            throw new Exception($"{kind}配置不能为空。");
        }

        var duplicate = fields.GroupBy(x => x, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1)?.Key;
        if (duplicate != null)
        {
            throw new Exception($"{kind}配置中字段重复: {duplicate}. 组合索引中的字段顺序必须明确且稳定。");
        }
    }

    private static void ValidateStableOrder(string[] fields, System.Collections.Generic.Dictionary<string, string> orderedSignatures, string kind)
    {
        if (fields.Length <= 1) return;

        var unorderedKey = string.Join("\u001F", fields.OrderBy(x => x, StringComparer.Ordinal));
        var orderedKey = string.Join("&", fields);
        if (orderedSignatures.TryGetValue(unorderedKey, out var existing) && !string.Equals(existing, orderedKey, StringComparison.Ordinal))
        {
            throw new Exception($"{kind}组合索引字段顺序不稳定: {orderedKey}. 已存在相同字段集合的顺序: {existing}。");
        }

        orderedSignatures[unorderedKey] = orderedKey;
    }

    private static void ValidateDictionaryKeyField(XField field, string kind)
    {
        var typeName = (field.TypeName ?? string.Empty).Trim();
        var lower = typeName.ToLowerInvariant();
        if (lower.StartsWith("array<", StringComparison.Ordinal) ||
            lower.StartsWith("map<", StringComparison.Ordinal) ||
            lower.StartsWith("json<", StringComparison.Ordinal) ||
            lower.StartsWith("custom<", StringComparison.Ordinal))
        {
            throw new Exception($"{kind}配置字段 {field.Name} 的类型 {field.TypeName} 不能作为字典 key。请改用基础类型或 enum<T>。");
        }
    }

}
