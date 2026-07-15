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

        public string[] SearchPatterns { get; set; } = ["*.*"];

        public string UsingNamespace { get; set; } = string.Empty;

        public string PrefixClassName { get; set; } = string.Empty;

        public string ImportNamespaces { get; set; } = string.Empty;

        public string FilterColumnTags { get; set; } = string.Empty;

        public bool ForceOverwrite { get; set; }

        public bool ValidateOnly { get; set; }

        public override bool Execute()
        {
            try
            {
                var searchPatterns = SearchPatterns.Length == 0 ? ["*.*"] : SearchPatterns;
                var generationMode = ValidateOnly ? GenerationMode.ValidateOnly : GenerationMode.CodeAndData;
                var result = new DataTableGenerator().GenerateFile(InputDirectories, searchPatterns, CodeOutputDirectory, DataOutputDirectory, UsingNamespace, PrefixClassName, ImportNamespaces, FilterColumnTags, ForceOverwrite, x => this.Log.LogMessage(x), generationMode).GetAwaiter().GetResult();
                if (!result.Succeeded)
                {
                    foreach (var failure in result.Failures)
                    {
                        this.Log.LogError(failure.ToString());
                    }

                    return false;
                }
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
