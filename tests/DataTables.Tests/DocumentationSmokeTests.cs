using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace DataTables.Tests;

public class DocumentationSmokeTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string ReadmePath = Path.Combine(RepositoryRoot, "README.md");

    [Fact]
    public void CliHelp_Should_Expose_KebabCase_LongOptions()
    {
        var result = RunCli("--help");

        result.ExitCode.Should().Be(0);
        result.Errors.Should().BeEmpty();
        var longOptions = GetLongOptions(result.Output);
        longOptions.Should().Contain([
            "--strict-name-validation",
            "--validate-formula-consistency",
            "--formula-policy",
            "--column-comment-marker-text",
            "--row-comment-marker-text",
            "--skip-cell-marker",
            "--array-nested-separators",
            "--diagnostics-json-output",
        ]);
        longOptions.Should().OnlyContain(option => option == option.ToLowerInvariant());
    }

    [Fact]
    public void Cli_Should_Reject_CamelCase_LongOption()
    {
        var result = RunCli("--strictNameValidation");

        result.ExitCode.Should().Be(1);
        result.Errors.Should().ContainSingle()
            .Which.Should().Contain("Argument '--strictNameValidation' is not recognized.");
    }

    [Fact]
    public void Readme_CSharpBlocks_Should_Compile()
    {
        var markdown = File.ReadAllText(ReadmePath);
        var snippets = GetFencedBlocks(markdown, "csharp").ToArray();

        snippets.Should().NotBeEmpty();
        for (var index = 0; index < snippets.Length; index++)
        {
            AssertCompiles($"ReadmeQuickStart{index + 1}", MakeCompilable(snippets[index]));
        }
    }

    [Fact]
    public void Readme_Dtgen_LongOptions_Should_Match_CliHelp()
    {
        var markdown = File.ReadAllText(ReadmePath);
        var documentedOptions = GetFencedBlocks(markdown, "bash")
            .Where(block => block.Contains("dotnet dtgen", StringComparison.Ordinal))
            .SelectMany(GetOptions)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var supportedOptions = GetOptions(RunCli("--help").Output)
            .Concat(GetOptions(RunCli("data", "--help").Output))
            .ToHashSet(StringComparer.Ordinal);

        documentedOptions.Should().NotBeEmpty();
        documentedOptions.Should().OnlyContain(option => supportedOptions.Contains(option),
            "README 中的 dtgen 长参数应与 CLI --help 完全一致");
    }

    private static CliResult RunCli(params string[] args)
    {
        var output = new List<string>();
        var errors = new List<string>();
        var exitCode = GeneratorCli.Run(args, output.Add, errors.Add);
        return new CliResult(exitCode, string.Join(Environment.NewLine, output), errors);
    }

    private static IReadOnlyCollection<string> GetLongOptions(string text)
        => Regex.Matches(text, @"(?<![A-Za-z0-9-])--[A-Za-z][A-Za-z0-9-]*")
            .Select(match => match.Value)
            .ToHashSet(StringComparer.Ordinal);

    private static IReadOnlyCollection<string> GetOptions(string text)
        => Regex.Matches(text, @"(?<![A-Za-z0-9-])-{1,2}[A-Za-z][A-Za-z0-9-]*")
            .Select(match => match.Value)
            .ToHashSet(StringComparer.Ordinal);

    private static IEnumerable<string> GetFencedBlocks(string markdown, string language)
    {
        var pattern = $@"(?ms)^```{Regex.Escape(language)}\s*\r?\n(?<code>.*?)^```\s*$";
        return Regex.Matches(markdown, pattern).Select(match => match.Groups["code"].Value);
    }

    private static string MakeCompilable(string snippet)
    {
        const string generatedTableStubs = """

public sealed class DTScene : DataTables.DataTableBase
{
    public DTScene() : base(string.Empty) { }
    public override System.Type Type => typeof(object);
    public override int Count => 0;
    public override bool ParseDataRow(int index, System.IO.BinaryReader reader) => true;
}

public sealed class DTItem : DataTables.DataTableBase
{
    public DTItem() : base(string.Empty) { }
    public override System.Type Type => typeof(object);
    public override int Count => 0;
    public override bool ParseDataRow(int index, System.IO.BinaryReader reader) => true;
}

namespace UnityEngine
{
    public class MonoBehaviour { }

    public static class Application
    {
        public static string streamingAssetsPath => string.Empty;
    }

    public static class Debug
    {
        public static void Log(object? value) { }
        public static void LogException(System.Exception exception) { }
    }
}
""";

        if (CSharpSyntaxTree.ParseText(snippet).GetRoot().DescendantNodes()
            .Any(node => node is Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax))
        {
            return snippet + generatedTableStubs;
        }

        return $$"""
using System;
using System.Threading;
using System.Threading.Tasks;
using DataTables;

public static class ReadmeSnippet
{
    public static async Task Run()
    {
        var cancellationToken = CancellationToken.None;
{{Indent(snippet, 8)}}
        await Task.CompletedTask;
    }
}
{{generatedTableStubs}}
""";
    }

    private static string Indent(string text, int spaces)
    {
        var indentation = new string(' ', spaces);
        return indentation + text.Replace("\n", "\n" + indentation, StringComparison.Ordinal);
    }

    private static void AssertCompiles(string assemblyName, string code)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(global::DataTables.DataTableBase).Assembly.Location));
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(code, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest))],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var assembly = new MemoryStream();
        var errors = compilation.Emit(assembly).Diagnostics
            .Where(diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .ToArray();

        errors.Should().BeEmpty($"README 代码块应可编译。生成的测试源码：{Environment.NewLine}{code}");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "DataTables.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the repository root from the test output directory.");
    }

    private sealed record CliResult(int ExitCode, string Output, IReadOnlyList<string> Errors);
}
