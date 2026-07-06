using System;
using System.Linq;

namespace DataTables.GeneratorCore;

public sealed class GraphTableParser : ITableSchemaParser
{
    private readonly RowTableParser m_RowTableParser = new();

    public string DataSetType => "graph";

    public int Parse(ISheetReader sheet, GenerationContext context, ParseOptions options, DiagnosticsCollector diagnostics)
    {
        var firstDataRowIndex = m_RowTableParser.Parse(sheet, context, options, diagnostics);
        if (firstDataRowIndex == -1)
        {
            return -1;
        }

        RequireField(context, "EdgeId");
        RequireField(context, "From");
        RequireField(context, "To");

        EnsureStringField(context, "EdgeId");
        EnsureStringField(context, "From");
        EnsureStringField(context, "To");

        if (context.GetField("Weight") is { } weightField && string.IsNullOrWhiteSpace(weightField.TypeName))
        {
            weightField.TypeName = "float";
        }

        AddBuiltInIndex(context.Indexs, "EdgeId");
        AddBuiltInGroup(context.Groups, "From");
        AddBuiltInGroup(context.Groups, "To");

        return firstDataRowIndex;
    }

    private static void RequireField(GenerationContext context, string fieldName)
    {
        if (context.GetField(fieldName) == null)
        {
            var available = string.Join(", ", context.Fields.Where(x => !x.IsIgnore).Select(x => x.Name));
            throw new FormatException($"DTGen=graph 缺少必需字段 {fieldName}. 可用字段: [{available}]");
        }
    }

    private static void EnsureStringField(GenerationContext context, string fieldName)
    {
        var field = context.GetField(fieldName)!;
        var typeName = (field.TypeName ?? string.Empty).Trim();
        if (typeName.Length == 0)
        {
            field.TypeName = "string";
            return;
        }

        if (!typeName.Equals("string", StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException($"DTGen=graph 字段 {fieldName} 必须声明为 string，当前为 {field.TypeName}");
        }
    }

    private static void AddBuiltInIndex(System.Collections.Generic.List<string[]> collection, string fieldName)
    {
        if (!collection.Any(x => x.Length == 1 && x[0].Equals(fieldName, StringComparison.Ordinal)))
        {
            collection.Add([fieldName]);
        }
    }

    private static void AddBuiltInGroup(System.Collections.Generic.List<string[]> collection, string fieldName)
    {
        if (!collection.Any(x => x.Length == 1 && x[0].Equals(fieldName, StringComparison.Ordinal)))
        {
            collection.Add([fieldName]);
        }
    }
}
