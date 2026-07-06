using System;
using System.Linq;

namespace DataTables.GeneratorCore;

public sealed class TreeTableParser : ITableSchemaParser
{
    private readonly RowTableParser m_RowTableParser = new();

    public string DataSetType => "tree";

    public int Parse(ISheetReader sheet, GenerationContext context, ParseOptions options, DiagnosticsCollector diagnostics)
    {
        var firstDataRowIndex = m_RowTableParser.Parse(sheet, context, options, diagnostics);
        if (firstDataRowIndex == -1)
        {
            return -1;
        }

        RequireField(context, "Id");
        RequireField(context, "ParentId");

        EnsureStringField(context, "Id");
        EnsureStringField(context, "ParentId");

        if (context.GetField("Order") is { } orderField)
        {
            var typeName = (orderField.TypeName ?? string.Empty).Trim();
            if (typeName.Length == 0)
            {
                orderField.TypeName = "int";
            }
        }

        AddBuiltInIndex(context.Indexs, "Id");
        AddBuiltInIndex(context.Groups, "ParentId");

        return firstDataRowIndex;
    }

    private static void RequireField(GenerationContext context, string fieldName)
    {
        if (context.GetField(fieldName) == null)
        {
            var available = string.Join(", ", context.Fields.Where(x => !x.IsIgnore).Select(x => x.Name));
            throw new FormatException($"DTGen=tree 缺少必需字段 {fieldName}. 可用字段: [{available}]");
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
            throw new FormatException($"DTGen=tree 字段 {fieldName} 必须声明为 string，当前为 {field.TypeName}");
        }
    }

    private static void AddBuiltInIndex(System.Collections.Generic.List<string[]> collection, string fieldName)
    {
        if (!collection.Any(x => x.Length == 1 && x[0].Equals(fieldName, StringComparison.Ordinal)))
        {
            collection.Add([fieldName]);
        }
    }
}
