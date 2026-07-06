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
                if (!string.Equals(group[i], field.Name, StringComparison.Ordinal)) group[i] = field.Name;
            }
        }
    }
}
