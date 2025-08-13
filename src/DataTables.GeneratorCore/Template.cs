using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DataTables.GeneratorCore;

public partial class DataTableManagerExtensionTemplate
{
    public string Namespace { get; set; } = string.Empty;

    public string DataRowPrefix { get; set; } = string.Empty;

    public IOrderedEnumerable<KeyValuePair<string, IOrderedEnumerable<string>>>? DataTables { get; set; }

    /// <summary>
    /// 每个完整表名到优先级字符串的映射（Critical/Normal/Lazy）
    /// </summary>
    public Dictionary<string, string>? TablePriorities { get; set; }
}

public partial class DataTableTemplate
{
    public GenerationContext GenerationContext { get; private set; }

    public string Using => string.Join(Environment.NewLine, this.GenerationContext.UsingStrings);

    public string Namespace => GenerationContext.Namespace;

    public string ClassName => GenerationContext.DataRowClassName;

    public DataTableTemplate(GenerationContext context)
    {
        this.GenerationContext = context;
    }

    internal string GetPropertyTypeString(XField property)
    {
        return DataTableProcessor.GetLanguageKeyword(property);
    }

    internal string GetDeserializeMethodString(XField property)
    {
        return DataTableProcessor.GetDeserializeMethodString(GenerationContext, property);
    }

    internal string BuildSummary(string summary)
    {
        var text = System.Security.SecurityElement.Escape(summary.Trim());
        var lines = text.Split('\n');
        if (lines.Length == 0)
        {
            return string.Empty;
        }
        else if (lines.Length == 1)
        {
            return lines[0].Trim();
        }
        else
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.Append("    /// ");
            sb.AppendLine(lines[0].Trim());
            for (int i = 1; i < lines.Length; i++)
            {
                sb.Append("    /// <para>");
                sb.Append(lines[i].Trim());
                sb.AppendLine("</para>");
            }
            sb.Append("    /// ");
            return sb.ToString();
        }
    }
}

public partial class DataMatrixTemplate
{
    internal const string kKey1 = "_key1";
    internal const string kKey2 = "_key2";
    internal const string kValue = "_value";

    public GenerationContext GenerationContext { get; private set; }

    public DataMatrixTemplate(GenerationContext generationContext)
    {
        this.GenerationContext = generationContext;
    }

    internal string BuildTypeString(string fieldName) => DataTableProcessor.GetLanguageKeyword(this.GenerationContext.GetField(fieldName)!);
    internal string BuildTypeValueString(string fieldName, string fieldValueString) => DataTableProcessor.GetLanguageValue(this.GenerationContext.GetField(fieldName)!, fieldValueString);
    internal string BuildDeserializeMethodString(string fieldName) => DataTableProcessor.GetDeserializeMethodString(GenerationContext, this.GenerationContext.GetField(fieldName)!);
}
