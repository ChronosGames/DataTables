using System.Collections.Generic;
using System.Text;

namespace DataTables.GeneratorCore;

public partial class DataTableManagerExtensionTemplate
{
    public string Namespace { get; set; }

    public string DataRowPrefix { get; set; }

    public Dictionary<string, string[]> DataTables { get; set; }
}

public partial class DataRowTemplate
{
    public string Using { get; set; }

    public GenerationContext GenerationContext { get; set; }

    public string Namespace => GenerationContext.Namespace;

    public string ClassName => GenerationContext.RealClassName;

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
            sb.Append("        /// ");
            sb.AppendLine(lines[0].Trim());
            for (int i = 1; i < lines.Length; i++)
            {
                sb.Append("        /// <para>");
                sb.Append(lines[i].Trim());
                sb.AppendLine("</para>");
            }
            sb.Append("        /// ");
            return sb.ToString();
        }
    }
}
