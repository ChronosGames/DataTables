using System;
using System.Diagnostics;
using DataTables.GeneratorCore;
using ConsoleAppFramework;

var app = ConsoleApp.Create();
app.Add<MyCommands>();
app.Run(args);

public class MyCommands
{
    /// <summary>
    /// 导出全部代码文件与配置数据
    /// </summary>
    /// <param name="inputDirectories">-i, Input file directory(search recursive).</param>
    /// <param name="searchPattern">-patterns, Input file wildcard.</param>
    /// <param name="codeOutputDir">-co, Code output file directory.</param>
    /// <param name="dataOutputDir">-do, Data output file directory.</param>
    /// <param name="importNamespaces">-ins, Import namespaces of generated files, split with char '&' for multiple namespaces.</param>
    /// <param name="usingNamespace">-n, Namespace of generated files.</param>
    /// <param name="prefixClassName">-p, Prefix of class names.</param>
    /// <param name="filterColumnTags">-t, Tags of filter columns.</param>
    /// <param name="forceOverwrite">-f, Overwrite generated files if the content is unchanged.</param>
    [Command("")]
    public void ExportAll(
        string[] inputDirectories,
        string[] searchPattern,
        string codeOutputDir,
        string dataOutputDir,
        string importNamespaces = "",
        string usingNamespace = "",
        string prefixClassName = "",
        string filterColumnTags = "",
        bool forceOverwrite = false)
    {
        var oldEncoding = Console.OutputEncoding;
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var sw = Stopwatch.StartNew();
        Console.WriteLine("Start DataTables CodeGeneration");

        try
        {
            new DataTableGenerator().GenerateFile(inputDirectories, searchPattern, codeOutputDir, dataOutputDir, usingNamespace, prefixClassName,
                importNamespaces: importNamespaces,
                filterColumnTags: filterColumnTags,
                forceOverwrite,
                Console.WriteLine);
        }
        catch (Exception e)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(e);
            Console.ResetColor();
        }

        Console.WriteLine("Complete DataTables Generation, elapsed: " + sw.Elapsed);

        Console.OutputEncoding = oldEncoding;
    }

    /// <summary>
    /// 导出单个数据文件
    /// </summary>
    /// <param name="inputDirectories">-i, Input file directory(search recursive).</param>
    /// <param name="searchPattern">-patterns, Input file wildcard.</param>
    /// <param name="dataOutputDir">-do, Data output file directory.</param>
    /// <param name="importNamespaces">-ins, Import namespaces of generated files, split with char '&' for multiple namespaces.</param>
    /// <param name="usingNamespace">-n, Namespace of generated files.</param>
    /// <param name="prefixClassName">-p, Prefix of class names.</param>
    /// <param name="filterColumnTags">-t, Tags of filter columns.</param>
    [Command("data")]
    public void ExportOne(
        string[] inputDirectories,
        string[] searchPattern,
        string dataOutputDir,
        string importNamespaces = "",
        string usingNamespace = "",
        string prefixClassName = "",
        string filterColumnTags = "")
    {
        var oldEncoding = Console.OutputEncoding;
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var sw = Stopwatch.StartNew();
        Console.WriteLine("Start DataTables CodeGeneration");

        try
        {
            new DataTableGenerator().GenerateFile(inputDirectories, searchPattern, string.Empty, dataOutputDir, usingNamespace, prefixClassName,
                importNamespaces: importNamespaces,
                filterColumnTags: filterColumnTags,
                true,
                Console.WriteLine);
        }
        catch (Exception e)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(e);
            Console.ResetColor();
        }

        Console.WriteLine("Complete DataTables Generation, elapsed: " + sw.Elapsed);

        Console.OutputEncoding = oldEncoding;
    }
}
