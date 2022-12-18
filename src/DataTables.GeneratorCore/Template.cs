using System;
using System.Collections.Generic;
using System.Text;

namespace DataTables.GeneratorCore
{
    public partial class DataTableManagerExtensionTemplate
    {
        public string Namespace { get; set; }
        public string[] ClassNames { get; set; }
    }

    public partial class DataRowTemplate
    {
        public string Using { get; set; }

        public GenerationContext GenerationContext { get; set; }

        public string Namespace => GenerationContext.Namespace;

        public string ClassName => GenerationContext.ClassName;

        internal string GetPropertyTypeString(Property property)
        {
            if (property.TypeName.StartsWith("Enum"))
            {
                return property.TypeName.Substring(4);
            }

            return property.TypeName;
        }

        internal string GetDeserializeMethodString(Property property)
        {
            return DataTableProcessor.GetDeserializeMethodString(GenerationContext, property);
        }
    }
}
