using System;
using DataTables.GeneratorCore;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace DataTables.MSBuild.Tasks
{
    public class DataTablesGenerator : Task
    {
        [Required]
        public string[] InputDirectories { get; set; } = Array.Empty<string>();
        [Required]
        public string CodeOutputDirectory { get; set; } = string.Empty;
        [Required]
        public string DataOutputDirectory { get; set; } = string.Empty;

        public string UsingNamespace { get; set; } = string.Empty;

        public string PrefixClassName { get; set; } = string.Empty;

        public string ImportNamespaces { get; set; } = string.Empty;

        public string FilterColumnTags { get; set; } = string.Empty;

        public bool ForceOverwrite { get; set; }

        public override bool Execute()
        {
            try
            {
                new DataTableGenerator().GenerateFile(InputDirectories, CodeOutputDirectory, DataOutputDirectory, UsingNamespace, PrefixClassName, ImportNamespaces, FilterColumnTags, ForceOverwrite, x => this.Log.LogMessage(x));
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
