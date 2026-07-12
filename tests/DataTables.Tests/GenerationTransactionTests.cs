using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DataTables.GeneratorCore;
using FluentAssertions;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using Xunit;

namespace DataTables.Tests;

public sealed class GenerationTransactionTests
{
    [Fact]
    public async Task FailedBatch_ShouldLeaveExistingOutputsUntouched()
    {
        var root = CreateTempDirectory();
        var input = Directory.CreateDirectory(Path.Combine(root, "input")).FullName;
        var code = Directory.CreateDirectory(Path.Combine(root, "code")).FullName;
        var data = Directory.CreateDirectory(Path.Combine(root, "data")).FullName;
        var existingCode = Path.Combine(code, "Existing.cs");
        var existingData = Path.Combine(data, "Existing.bytes");
        await File.WriteAllTextAsync(existingCode, "existing-code");
        await File.WriteAllBytesAsync(existingData, new byte[] { 9, 8, 7 });
        await CreateValidWorkbookAsync(Path.Combine(input, "valid.xlsx"));
        await File.WriteAllTextAsync(Path.Combine(input, "invalid.xlsx"), "not an Excel workbook");

        var result = await GenerateAsync(input, code, data);

        result.Succeeded.Should().BeFalse();
        Directory.GetFiles(code, "*", SearchOption.AllDirectories).Should().Equal(existingCode);
        Directory.GetFiles(data, "*", SearchOption.AllDirectories).Should().Equal(existingData);
        (await File.ReadAllTextAsync(existingCode)).Should().Be("existing-code");
        (await File.ReadAllBytesAsync(existingData)).Should().Equal(9, 8, 7);
        Directory.EnumerateDirectories(code, ".dtgen-*", SearchOption.TopDirectoryOnly).Should().BeEmpty();
        Directory.EnumerateDirectories(data, ".dtgen-*", SearchOption.TopDirectoryOnly).Should().BeEmpty();
    }

    [Fact]
    public async Task SuccessfulBatch_ShouldCommitCodeDataAndManagerTogether()
    {
        var root = CreateTempDirectory();
        var input = Directory.CreateDirectory(Path.Combine(root, "input")).FullName;
        var code = Directory.CreateDirectory(Path.Combine(root, "code")).FullName;
        var data = Directory.CreateDirectory(Path.Combine(root, "data")).FullName;
        await CreateValidWorkbookAsync(Path.Combine(input, "valid.xlsx"));

        var result = await GenerateAsync(input, code, data);

        result.Succeeded.Should().BeTrue();
        Directory.GetFiles(code, "*.cs").Select(Path.GetFileName).Should().BeEquivalentTo("DRItem.cs", "DataTableManagerExtension.cs");
        Directory.GetFiles(data, "*.bytes").Should().ContainSingle();
        Directory.EnumerateDirectories(code, ".dtgen-*", SearchOption.TopDirectoryOnly).Should().BeEmpty();
        Directory.EnumerateDirectories(data, ".dtgen-*", SearchOption.TopDirectoryOnly).Should().BeEmpty();
    }

    [Fact]
    public async Task UnchangedBatch_ShouldCompareContentAndPreserveCommittedFiles()
    {
        var root = CreateTempDirectory();
        var input = Directory.CreateDirectory(Path.Combine(root, "input")).FullName;
        var code = Directory.CreateDirectory(Path.Combine(root, "code")).FullName;
        var data = Directory.CreateDirectory(Path.Combine(root, "data")).FullName;
        await CreateValidWorkbookAsync(Path.Combine(input, "valid.xlsx"));
        (await GenerateAsync(input, code, data)).Succeeded.Should().BeTrue();
        var files = Directory.GetFiles(code).Concat(Directory.GetFiles(data)).ToArray();
        var timestamps = files.ToDictionary(path => path, File.GetLastWriteTimeUtc);

        var logs = new System.Collections.Generic.List<string>();
        var result = await GenerateAsync(input, code, data, forceOverwrite: false, logger: logs.Add);

        result.Succeeded.Should().BeTrue();
        result.SkippedCount.Should().Be(1);
        logs.Should().NotContain(message => message.Contains("Generate Excel File:"));
        files.Should().OnlyContain(path => File.GetLastWriteTimeUtc(path) == timestamps[path]);
    }

    [Fact]
    public async Task ChangedWorkbook_ShouldNotParseUnchangedWorkbook()
    {
        var root = CreateTempDirectory();
        var input = Directory.CreateDirectory(Path.Combine(root, "input")).FullName;
        var code = Directory.CreateDirectory(Path.Combine(root, "code")).FullName;
        var data = Directory.CreateDirectory(Path.Combine(root, "data")).FullName;
        var itemWorkbook = Path.Combine(input, "items.xlsx");
        var monsterWorkbook = Path.Combine(input, "monsters.xlsx");
        await CreateValidWorkbookAsync(itemWorkbook, "Item", 1);
        await CreateValidWorkbookAsync(monsterWorkbook, "Monster", 1);
        (await GenerateAsync(input, code, data)).Succeeded.Should().BeTrue();
        var unchangedCode = Path.Combine(code, "DRMonster.cs");
        var unchangedData = Path.Combine(data, "DataTables.Tests.Generated.DTMonster.bytes");
        var unchangedCodeTimestamp = File.GetLastWriteTimeUtc(unchangedCode);
        var unchangedDataTimestamp = File.GetLastWriteTimeUtc(unchangedData);

        await CreateValidWorkbookAsync(itemWorkbook, "Item", 2);
        var logs = new System.Collections.Generic.List<string>();
        var result = await GenerateAsync(input, code, data, forceOverwrite: false, logger: logs.Add);

        result.Succeeded.Should().BeTrue();
        logs.Count(message => message.Contains("Generate Excel File:")).Should().Be(1);
        logs.Should().Contain(message => message.Contains("items.xlsx"));
        logs.Should().NotContain(message => message.Contains("monsters.xlsx"));
        File.GetLastWriteTimeUtc(unchangedCode).Should().Be(unchangedCodeTimestamp);
        File.GetLastWriteTimeUtc(unchangedData).Should().Be(unchangedDataTimestamp);
    }

    [Fact]
    public async Task RemovedWorkbook_ShouldDeleteOwnedOutputsAndManagerRegistration()
    {
        var root = CreateTempDirectory();
        var input = Directory.CreateDirectory(Path.Combine(root, "input")).FullName;
        var code = Directory.CreateDirectory(Path.Combine(root, "code")).FullName;
        var data = Directory.CreateDirectory(Path.Combine(root, "data")).FullName;
        var itemWorkbook = Path.Combine(input, "items.xlsx");
        var monsterWorkbook = Path.Combine(input, "monsters.xlsx");
        await CreateValidWorkbookAsync(itemWorkbook, "Item", 1);
        await CreateValidWorkbookAsync(monsterWorkbook, "Monster", 1);
        (await GenerateAsync(input, code, data)).Succeeded.Should().BeTrue();

        File.Delete(monsterWorkbook);
        var result = await GenerateAsync(input, code, data, forceOverwrite: false);

        result.Succeeded.Should().BeTrue();
        File.Exists(Path.Combine(code, "DRMonster.cs")).Should().BeFalse();
        File.Exists(Path.Combine(data, "DataTables.Tests.Generated.DTMonster.bytes")).Should().BeFalse();
        (await File.ReadAllTextAsync(Path.Combine(code, "DataTableManagerExtension.cs"))).Should().NotContain("DTMonster");
    }

    [Fact]
    public async Task FailedIncrementalBatch_ShouldPreserveOutputsAndManifest()
    {
        var root = CreateTempDirectory();
        var input = Directory.CreateDirectory(Path.Combine(root, "input")).FullName;
        var code = Directory.CreateDirectory(Path.Combine(root, "code")).FullName;
        var data = Directory.CreateDirectory(Path.Combine(root, "data")).FullName;
        var workbook = Path.Combine(input, "items.xlsx");
        await CreateValidWorkbookAsync(workbook);
        (await GenerateAsync(input, code, data)).Succeeded.Should().BeTrue();
        var output = Path.Combine(data, "DataTables.Tests.Generated.DTItem.bytes");
        var manifest = Path.Combine(data, ".dtgen-manifest.json");
        var outputBytes = await File.ReadAllBytesAsync(output);
        var manifestText = await File.ReadAllTextAsync(manifest);

        await File.WriteAllTextAsync(workbook, "not an Excel workbook");
        var result = await GenerateAsync(input, code, data, forceOverwrite: false);

        result.Succeeded.Should().BeFalse();
        (await File.ReadAllBytesAsync(output)).Should().Equal(outputBytes);
        (await File.ReadAllTextAsync(manifest)).Should().Be(manifestText);
    }

    [Fact]
    public async Task MissingOutput_ShouldRegenerateOwningWorkbook()
    {
        var root = CreateTempDirectory();
        var input = Directory.CreateDirectory(Path.Combine(root, "input")).FullName;
        var code = Directory.CreateDirectory(Path.Combine(root, "code")).FullName;
        var data = Directory.CreateDirectory(Path.Combine(root, "data")).FullName;
        await CreateValidWorkbookAsync(Path.Combine(input, "items.xlsx"));
        (await GenerateAsync(input, code, data)).Succeeded.Should().BeTrue();
        var dataFile = Path.Combine(data, "DataTables.Tests.Generated.DTItem.bytes");
        File.Delete(dataFile);
        var logs = new System.Collections.Generic.List<string>();

        var result = await GenerateAsync(input, code, data, forceOverwrite: false, logger: logs.Add);

        result.Succeeded.Should().BeTrue();
        File.Exists(dataFile).Should().BeTrue();
        logs.Should().Contain(message => message.Contains("Generate Excel File:"));
    }

    [Fact]
    public async Task GeneratorOptionChange_ShouldRegenerateAndDeleteOldOutput()
    {
        var root = CreateTempDirectory();
        var input = Directory.CreateDirectory(Path.Combine(root, "input")).FullName;
        var code = Directory.CreateDirectory(Path.Combine(root, "code")).FullName;
        var data = Directory.CreateDirectory(Path.Combine(root, "data")).FullName;
        await CreateValidWorkbookAsync(Path.Combine(input, "items.xlsx"));
        (await GenerateAsync(input, code, data)).Succeeded.Should().BeTrue();

        var result = await GenerateAsync(input, code, data, forceOverwrite: false, dataRowClassPrefix: "Row");

        result.Succeeded.Should().BeTrue();
        File.Exists(Path.Combine(code, "DRItem.cs")).Should().BeFalse();
        File.Exists(Path.Combine(code, "RowItem.cs")).Should().BeTrue();
    }

    [Fact]
    public async Task ModifiedOutput_ShouldRegenerateOwningWorkbook()
    {
        var root = CreateTempDirectory();
        var input = Directory.CreateDirectory(Path.Combine(root, "input")).FullName;
        var code = Directory.CreateDirectory(Path.Combine(root, "code")).FullName;
        var data = Directory.CreateDirectory(Path.Combine(root, "data")).FullName;
        await CreateValidWorkbookAsync(Path.Combine(input, "items.xlsx"));
        (await GenerateAsync(input, code, data)).Succeeded.Should().BeTrue();
        var codeFile = Path.Combine(code, "DRItem.cs");
        await File.AppendAllTextAsync(codeFile, "// modified");
        var logs = new System.Collections.Generic.List<string>();

        var result = await GenerateAsync(input, code, data, forceOverwrite: false, logger: logs.Add);

        result.Succeeded.Should().BeTrue();
        (await File.ReadAllTextAsync(codeFile)).Should().NotContain("// modified");
        logs.Should().Contain(message => message.Contains("Generate Excel File:"));
    }

    private static Task<GenerationResult> GenerateAsync(string input, string code, string data, bool forceOverwrite = true, Action<string>? logger = null, string dataRowClassPrefix = "DR")
    {
        return new DataTableGenerator().GenerateFile(
            inputDirectories: new[] { input },
            searchPatterns: new[] { "*.xlsx" },
            codeOutputDir: code,
            dataOutputDir: data,
            usingNamespace: "DataTables.Tests.Generated",
            dataRowClassPrefix: dataRowClassPrefix,
            importNamespaces: string.Empty,
            filterColumnTags: string.Empty,
            forceOverwrite: forceOverwrite,
            logger: logger ?? (_ => { }));
    }

    private static async Task CreateValidWorkbookAsync(string path, string className = "Item", int id = 1)
    {
        using var workbook = new XSSFWorkbook();
        var sheet = workbook.CreateSheet("Items");
        sheet.CreateRow(0).CreateCell(0, CellType.String).SetCellValue($"dtgen=table, class={className}");
        sheet.CreateRow(1).CreateCell(0, CellType.String).SetCellValue("Identifier");
        sheet.CreateRow(2).CreateCell(0, CellType.String).SetCellValue("Id");
        sheet.CreateRow(3).CreateCell(0, CellType.String).SetCellValue("int");
        sheet.CreateRow(4).CreateCell(0, CellType.Numeric).SetCellValue(id);
        await using var stream = File.Create(path);
        workbook.Write(stream);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "dt_generation_transaction_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
