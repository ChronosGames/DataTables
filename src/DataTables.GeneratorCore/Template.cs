using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DataTables.GeneratorCore
{
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

        internal string GetPropertyTypeString(Property property)
        {
            return DataTableProcessor.GetLanguageKeyword(property);
        }

        internal string GetDeserializeMethodString(Property property)
        {
            return DataTableProcessor.GetDeserializeMethodString(GenerationContext, property);
        }

        internal string BuildSummary(string summary)
        {
            var arr = summary.Split('\n');
            if (arr.Length == 0)
            {
                return string.Empty;
            }
            else if (arr.Length == 1)
            {
                return arr[0].Trim();
            }
            else
            {
                var sb = new StringBuilder();
                sb.AppendLine();
                sb.Append("        /// ");
                sb.AppendLine(arr[0].Trim());
                for (int i = 1; i < arr.Length; i++)
                {
                    sb.Append("        /// <para>");
                    sb.Append(arr[i].Trim());
                    sb.AppendLine("</para>");
                }
                sb.Append("        /// ");
                return sb.ToString();
            }
        }
    }
}
