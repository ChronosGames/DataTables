using System;
using System.Diagnostics;
using System.Threading.Tasks;
using DataTables.GeneratorCore;
using Microsoft.Extensions.Hosting;

namespace DataTables.Generator;

public class Program : ConsoleAppBase
{
    static async Task Main(string[] args)
    {
        await Host.CreateDefaultBuilder()
            .ConfigureLogging(x => x.ReplaceToSimpleConsole())
            .RunConsoleAppFrameworkAsync<Program>(args);
    }

    public void Execute(
        [Option("i", "Input file directory(search recursive).")] string inputDirectory,
        [Option("co", "Code output file directory.")] string codeOutputDir,
        [Option("do", "Data output file directory.")] string dataOutputDir,
        [Option("ins", "Import namespaces of generated files, split with char '&' for multiple namespaces.")] string importNamespaces = "",
        [Option("n", "Namespace of generated files.")] string usingNamespace = "",
        [Option("p", "Prefix of class names.")] string prefixClassName = "",
        [Option("t", "Tags of filter columns.")] string filterColumnTags = "",
        [Option("f", "Overwrite generated files if the content is unchanged.")] bool forceOverwrite = false)
    {
        var oldEncoding = Console.OutputEncoding;
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var sw = Stopwatch.StartNew();
        Console.WriteLine("Start DataTables CodeGeneration");

        try
        {
            new DataTableGenerator().GenerateFile(inputDirectory, codeOutputDir, dataOutputDir, usingNamespace, prefixClassName,
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
}
