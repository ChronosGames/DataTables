using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;

namespace DataTables.MSBuild.Tasks
{
    public class DataTablesGenerator : Task
    {
        [Required]
        public string InputDirectory { get; set; }
        [Required]
        public string CodeOutputDirectory { get; set; }
        [Required]
        public string DataOutputDirectory { get; set; }

        public string UsingNamespace { get; set; }

        public string PrefixClassName { get; set; }

        public string ImportNamespaces { get; set; }

        public string FilterColumnTags { get; set; }

        public bool ForceOverwrite { get; set; }

        public override bool Execute()
        {
            try
            {
                new DataTables.GeneratorCore.DataTableGenerator().GenerateFile(InputDirectory, CodeOutputDirectory, DataOutputDirectory, UsingNamespace, PrefixClassName, ImportNamespaces, FilterColumnTags, ForceOverwrite, x => this.Log.LogMessage(x));
            }
            catch (Exception ex)
            {
                this.Log.LogErrorFromException(ex, true);
                return false;
            }
            return true;
        }
    }
}