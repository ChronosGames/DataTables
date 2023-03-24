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
        public string[] DataRowTypeName { get; set; }
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
    }
}
