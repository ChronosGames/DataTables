using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DataTables.GeneratorCore;
using FluentAssertions;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using Xunit;

namespace DataTables.Tests;

public sealed class TableTagFilteringTests
{
    public static IEnumerable<object[]> BooleanExpressions()
    {
        yield return ["C && !S", "Always,Client"];
        yield return ["S || C", "Always,Client,Server,Shared"];
        yield return ["NOT C", "Always,Server"];
    }

    [Theory]
    [MemberData(nameof(BooleanExpressions))]
    public async Task TableTags_ShouldUseBooleanExpressionWhileAlwaysIncludingUntaggedTables(string expression, string expectedNames)
    {
        var paths = CreatePaths();
        await CreateWorkbookAsync(Path.Combine(paths.Input, "tags.xlsx"), workbook =>
        {
            AddTableSheet(workbook, "Always", "Always");
            AddTableSheet(workbook, "Server", "Server", tags: "S");
            AddTableSheet(workbook, "Client", "Client", tags: "C");
            AddTableSheet(workbook, "Shared", "Shared", tags: "S&C");
        });

        var result = await GenerateAsync(paths, expression);

        result.Succeeded.Should().BeTrue();
        GeneratedRows(paths.Code).Should().Equal(expectedNames.Split(',').OrderBy(name => name, StringComparer.Ordinal));
        result.SkippedCount.Should().Be(4 - expectedNames.Split(',').Length);
    }

    [Fact]
    public async Task DisableTagsFilter_ShouldDisableTableAndFieldFilteringTogether()
    {
        var paths = CreatePaths();
        await CreateWorkbookAsync(Path.Combine(paths.Input, "disabled.xlsx"), workbook =>
            AddTableSheet(workbook, "Disabled", "Disabled", tags: "S", disableTagsFilter: true, includeTaggedField: true));

        var result = await GenerateAsync(paths, "C");

        result.Succeeded.Should().BeTrue();
        var code = await File.ReadAllTextAsync(Path.Combine(paths.Code, "DRDisabled.cs"));
        code.Should().Contain("public string Secret");
    }

    [Fact]
    public async Task SplitLogicalTables_ShouldApplyTagsIndependently()
    {
        var paths = CreatePaths();
        await CreateWorkbookAsync(Path.Combine(paths.Input, "split.xlsx"), workbook =>
        {
            AddTableSheet(workbook, "Server", "Split", tags: "S", child: "server");
            AddTableSheet(workbook, "Client", "Split", tags: "C", child: "client");
        });

        var result = await GenerateAsync(paths, "C");

        result.Succeeded.Should().BeTrue();
        File.Exists(Path.Combine(paths.Data, "DataTables.Tests.Tags.DTSplit.client.bytes")).Should().BeTrue();
        File.Exists(Path.Combine(paths.Data, "DataTables.Tests.Tags.DTSplit.server.bytes")).Should().BeFalse();
        var manager = await File.ReadAllTextAsync(Path.Combine(paths.Code, "DataTableManagerExtension.cs"));
        manager.Should().Contain("\"client\"").And.NotContain("\"server\"");
    }

    [Fact]
    public async Task ChangingTableFilter_ShouldTransactionallyRemoveStaleOutputsAndManifestEntries()
    {
        var paths = CreatePaths();
        await CreateWorkbookAsync(Path.Combine(paths.Input, "switch.xlsx"), workbook =>
        {
            AddTableSheet(workbook, "Server", "Server", tags: "S");
            AddTableSheet(workbook, "Client", "Client", tags: "C");
        });
        (await GenerateAsync(paths, "S")).Succeeded.Should().BeTrue();

        var result = await GenerateAsync(paths, "C", forceOverwrite: false);

        result.Succeeded.Should().BeTrue();
        File.Exists(Path.Combine(paths.Code, "DRServer.cs")).Should().BeFalse();
        File.Exists(Path.Combine(paths.Data, "DataTables.Tests.Tags.DTServer.bytes")).Should().BeFalse();
        File.Exists(Path.Combine(paths.Code, "DRClient.cs")).Should().BeTrue();
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(paths.Data, "manifest.json")));
        manifest.RootElement.GetProperty("entries").EnumerateArray()
            .Select(entry => entry.GetProperty("name").GetString())
            .Should().Equal("DataTables.Tests.Tags.DTClient");
    }

    [Fact]
    public async Task ValidateOnly_ShouldHonorTableTagsWithoutWritingArtifacts()
    {
        var paths = CreatePaths();
        await CreateWorkbookAsync(Path.Combine(paths.Input, "validate.xlsx"), workbook =>
        {
            AddTableSheet(workbook, "Server", "Server", tags: "S");
            AddTableSheet(workbook, "Client", "Client", tags: "C");
        });

        var result = await GenerateAsync(paths, "C", generationMode: GenerationMode.ValidateOnly);

        result.Succeeded.Should().BeTrue();
        result.SucceededCount.Should().Be(1);
        result.SkippedCount.Should().Be(1);
        Directory.GetFiles(paths.Code, "*", SearchOption.AllDirectories).Should().BeEmpty();
        Directory.GetFiles(paths.Data, "*", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Fact]
    public async Task InvalidTableTagDeclaration_ShouldReportA1Error()
    {
        var paths = CreatePaths();
        await CreateWorkbookAsync(Path.Combine(paths.Input, "invalid-tags.xlsx"), workbook =>
            AddTableSheet(workbook, "Invalid", "Invalid", tags: "S+C"));

        var result = await GenerateAsync(paths, "C");

        result.Succeeded.Should().BeFalse();
        result.Diagnostics.Should().ContainSingle(diagnostic =>
            diagnostic.Severity == DiagnosticSeverity.Error
            && diagnostic.Cell == "A1"
            && diagnostic.Message.Contains("无效的表标签声明", StringComparison.Ordinal));
        Directory.GetFiles(paths.Code, "*", SearchOption.AllDirectories).Should().BeEmpty();
        Directory.GetFiles(paths.Data, "*", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Theory]
    [InlineData(GenerationMode.CodeAndData)]
    [InlineData(GenerationMode.ValidateOnly)]
    public async Task InvalidGlobalExpression_ShouldBeAnErrorForExportAndValidate(GenerationMode generationMode)
    {
        var paths = CreatePaths();
        await CreateWorkbookAsync(Path.Combine(paths.Input, "invalid-filter.xlsx"), workbook =>
            AddTableSheet(workbook, "Disabled", "Disabled", tags: "S", disableTagsFilter: true));

        var result = await GenerateAsync(paths, "C &&", generationMode: generationMode);

        result.Succeeded.Should().BeFalse();
        result.Diagnostics.Should().ContainSingle(diagnostic =>
            diagnostic.Severity == DiagnosticSeverity.Error
            && diagnostic.Message.Contains("无效的标签过滤表达式", StringComparison.Ordinal));
        Directory.GetFiles(paths.Code, "*", SearchOption.AllDirectories).Should().BeEmpty();
        Directory.GetFiles(paths.Data, "*", SearchOption.AllDirectories).Should().BeEmpty();
    }

    private static string[] GeneratedRows(string codeDirectory)
    {
        return Directory.GetFiles(codeDirectory, "DR*.cs")
            .Select(Path.GetFileNameWithoutExtension)
            .Select(name => name![2..])
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }

    private static Task<GenerationResult> GenerateAsync(
        TestPaths paths,
        string expression,
        bool forceOverwrite = true,
        GenerationMode generationMode = GenerationMode.CodeAndData)
    {
        return new DataTableGenerator().GenerateFile(
            inputDirectories: [paths.Input],
            searchPatterns: ["*.xlsx"],
            codeOutputDir: paths.Code,
            dataOutputDir: paths.Data,
            usingNamespace: "DataTables.Tests.Tags",
            dataRowClassPrefix: "DR",
            importNamespaces: string.Empty,
            filterColumnTags: expression,
            forceOverwrite: forceOverwrite,
            logger: _ => { },
            generationMode: generationMode);
    }

    private static async Task CreateWorkbookAsync(string path, Action<XSSFWorkbook> configure)
    {
        using var workbook = new XSSFWorkbook();
        configure(workbook);
        await using var stream = File.Create(path);
        workbook.Write(stream);
    }

    private static void AddTableSheet(
        XSSFWorkbook workbook,
        string sheetName,
        string className,
        string? tags = null,
        bool disableTagsFilter = false,
        string? child = null,
        bool includeTaggedField = false)
    {
        var sheet = workbook.CreateSheet(sheetName);
        var settings = $"dtgen=table, class={className}";
        if (!string.IsNullOrEmpty(tags)) settings += $", tags={tags}";
        if (disableTagsFilter) settings += ", disabletagsfilter";
        if (!string.IsNullOrEmpty(child)) settings += $", child={child}";
        sheet.CreateRow(0).CreateCell(0, CellType.String).SetCellValue(settings);
        var titles = sheet.CreateRow(1);
        var names = sheet.CreateRow(2);
        var types = sheet.CreateRow(3);
        var values = sheet.CreateRow(4);
        titles.CreateCell(0, CellType.String).SetCellValue("Identifier");
        names.CreateCell(0, CellType.String).SetCellValue("Id");
        types.CreateCell(0, CellType.String).SetCellValue("int");
        values.CreateCell(0, CellType.Numeric).SetCellValue(1);
        if (includeTaggedField)
        {
            titles.CreateCell(1, CellType.String).SetCellValue("Secret@S");
            names.CreateCell(1, CellType.String).SetCellValue("Secret");
            types.CreateCell(1, CellType.String).SetCellValue("string");
            values.CreateCell(1, CellType.String).SetCellValue("kept");
        }
    }

    private static TestPaths CreatePaths()
    {
        var root = Path.Combine(Path.GetTempPath(), "dt_table_tags_" + Guid.NewGuid().ToString("N"));
        return new TestPaths(
            Directory.CreateDirectory(Path.Combine(root, "input")).FullName,
            Directory.CreateDirectory(Path.Combine(root, "code")).FullName,
            Directory.CreateDirectory(Path.Combine(root, "data")).FullName);
    }

    private sealed record TestPaths(string Input, string Code, string Data);
}
