using System;
using System.Diagnostics;
using System.Threading.Tasks;
using DataTables.GeneratorCore;
using ConsoleAppFramework;

return GeneratorCli.Run(args);

public static class GeneratorCli
{
    public static int Run(string[] args, Action<string>? writeLine = null, Action<string>? writeError = null)
    {
        var oldExitCode = Environment.ExitCode;
        var oldLog = ConsoleApp.Log;
        var oldLogError = ConsoleApp.LogError;

        try
        {
            Environment.ExitCode = 0;
            if (writeLine != null)
            {
                ConsoleApp.Log = writeLine;
            }
            if (writeError != null)
            {
                ConsoleApp.LogError = writeError;
            }

            var app = ConsoleApp.Create();
            app.Add<MyCommands>();
            app.Run(args);
            return Environment.ExitCode;
        }
        finally
        {
            ConsoleApp.Log = oldLog;
            ConsoleApp.LogError = oldLogError;
            Environment.ExitCode = oldExitCode;
        }
    }
}

public class MyCommands
{
    /// <summary>
    /// 导出全部代码文件与配置数据
    /// </summary>
    /// <param name="inputDirectories">-i, Input file directory(search recursive).</param>
    /// <param name="searchPattern">-patterns, Input file wildcard.</param>
    /// <param name="codeOutputDir">-co, Code output file directory.</param>
    /// <param name="dataOutputDir">-do, Data output file directory.</param>
    /// <param name="importNamespaces">-ins, Import namespaces of generated files, split with char '&amp;' for multiple namespaces.</param>
    /// <param name="usingNamespace">-n, Namespace of generated files.</param>
    /// <param name="prefixClassName">-p, Prefix of class names.</param>
    /// <param name="filterColumnTags">-t, Tags of filter columns.</param>
    /// <param name="forceOverwrite">-f, Overwrite generated files if the content is unchanged.</param>
    /// <param name="skipCellMarker">-skipCellMarker, Skip cell marker text.</param>
    /// <param name="arrayNestedSeparators">-arrayNestedSeparators, Separators per array nesting depth for plain-text arrays (e.g. "#|-" means depth1='#', depth2='|', depth3='-'). Empty keeps legacy behavior (prefer '|' else '#').</param>
    [Command("")]
    public async Task ExportAll(
        string[] inputDirectories,
        string[] searchPattern,
        string codeOutputDir,
        string dataOutputDir,
        string importNamespaces = "",
        string usingNamespace = "",
        string prefixClassName = "",
        string filterColumnTags = "",
        bool forceOverwrite = false,
        // ParseOptions
        bool strictNameValidation = true,
        bool validateFormulaConsistency = true,
        string formulaPolicy = "ValidateOnly", // Off|ValidateOnly|ForceEvaluate
        string columnCommentMarkerText = "#列注释标志",
        string rowCommentMarkerText = "#行注释标志",
        string skipCellMarker = "#",
        string arrayNestedSeparators = "",
        string diagnosticsJsonOutput = "")
    {
        var sw = Stopwatch.StartNew();
        Console.WriteLine("Start DataTables CodeGeneration");

        var options = CreateParseOptions(filterColumnTags, strictNameValidation, validateFormulaConsistency, formulaPolicy, columnCommentMarkerText, rowCommentMarkerText, skipCellMarker, arrayNestedSeparators);
        var result = await new DataTableGenerator().GenerateFile(inputDirectories, searchPattern, codeOutputDir, dataOutputDir, usingNamespace, prefixClassName,
            importNamespaces: importNamespaces,
            filterColumnTags: filterColumnTags,
            forceOverwrite,
            Console.WriteLine,
            GenerationMode.CodeAndData,
            options,
            string.IsNullOrWhiteSpace(diagnosticsJsonOutput) ? null : diagnosticsJsonOutput);

        if (!result.Succeeded)
        {
            Environment.ExitCode = 1;
            return;
        }

        Console.WriteLine("Complete DataTables Generation, elapsed: " + sw.Elapsed);
    }

    /// <summary>
    /// 导出单个数据文件
    /// </summary>
    /// <param name="inputDirectories">-i, Input file directory(search recursive).</param>
    /// <param name="searchPattern">-patterns, Input file wildcard.</param>
    /// <param name="dataOutputDir">-do, Data output file directory.</param>
    /// <param name="importNamespaces">-ins, Import namespaces of generated files, split with char '&amp;' for multiple namespaces.</param>
    /// <param name="usingNamespace">-n, Namespace of generated files.</param>
    /// <param name="prefixClassName">-p, Prefix of class names.</param>
    /// <param name="filterColumnTags">-t, Tags of filter columns.</param>
    [Command("data")]
    public async Task ExportOne(
        string[] inputDirectories,
        string[] searchPattern,
        string dataOutputDir,
        string importNamespaces = "",
        string usingNamespace = "",
        string prefixClassName = "",
        string filterColumnTags = "",
        // ParseOptions
        bool strictNameValidation = true,
        bool validateFormulaConsistency = true,
        string formulaPolicy = "ValidateOnly",
        string columnCommentMarkerText = "#列注释标志",
        string rowCommentMarkerText = "#行注释标志",
        string skipCellMarker = "#",
        string arrayNestedSeparators = "",
        string diagnosticsJsonOutput = "")
    {
        var oldEncoding = Console.OutputEncoding;
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var sw = Stopwatch.StartNew();
        Console.WriteLine("Start DataTables CodeGeneration");

        var options = CreateParseOptions(filterColumnTags, strictNameValidation, validateFormulaConsistency, formulaPolicy, columnCommentMarkerText, rowCommentMarkerText, skipCellMarker, arrayNestedSeparators);
        var result = await new DataTableGenerator().GenerateFile(inputDirectories, searchPattern, string.Empty, dataOutputDir, usingNamespace, prefixClassName,
            importNamespaces: importNamespaces,
            filterColumnTags: filterColumnTags,
            true,
            Console.WriteLine,
            GenerationMode.DataOnly,
            options,
            string.IsNullOrWhiteSpace(diagnosticsJsonOutput) ? null : diagnosticsJsonOutput);

        if (!result.Succeeded)
        {
            Environment.ExitCode = 1;
            return;
        }

        Console.WriteLine("Complete DataTables Generation, elapsed: " + sw.Elapsed);

        Console.OutputEncoding = oldEncoding;
    }

    private static ParseOptions CreateParseOptions(string filterColumnTags, bool strictNameValidation, bool validateFormulaConsistency, string formulaPolicy, string columnCommentMarkerText, string rowCommentMarkerText, string skipCellMarker, string arrayNestedSeparators)
    {
        return new ParseOptions
        {
            FilterColumnTags = filterColumnTags,
            StrictNameValidation = strictNameValidation,
            ValidateFormulaConsistency = validateFormulaConsistency,
            FormulaPolicy = formulaPolicy.Equals("off", StringComparison.OrdinalIgnoreCase) ? FormulaEvaluationPolicy.Off
                : formulaPolicy.Equals("forceevaluate", StringComparison.OrdinalIgnoreCase) ? FormulaEvaluationPolicy.ForceEvaluate
                : FormulaEvaluationPolicy.ValidateOnly,
            ColumnCommentMarkerText = columnCommentMarkerText,
            RowCommentMarkerText = rowCommentMarkerText,
            SkipCellMarker = skipCellMarker,
            ArrayNestedSeparators = arrayNestedSeparators ?? string.Empty,
        };
    }
}
