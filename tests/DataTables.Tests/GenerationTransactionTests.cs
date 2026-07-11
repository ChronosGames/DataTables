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

        var result = await GenerateAsync(input, code, data, forceOverwrite: false);

        result.Succeeded.Should().BeTrue();
        result.SkippedCount.Should().Be(1);
        files.Should().OnlyContain(path => File.GetLastWriteTimeUtc(path) == timestamps[path]);
    }

    private static Task<GenerationResult> GenerateAsync(string input, string code, string data, bool forceOverwrite = true)
    {
        return new DataTableGenerator().GenerateFile(
            inputDirectories: new[] { input },
            searchPatterns: new[] { "*.xlsx" },
            codeOutputDir: code,
            dataOutputDir: data,
            usingNamespace: "DataTables.Tests.Generated",
            dataRowClassPrefix: "DR",
            importNamespaces: string.Empty,
            filterColumnTags: string.Empty,
            forceOverwrite: forceOverwrite,
            logger: _ => { });
    }

    private static async Task CreateValidWorkbookAsync(string path)
    {
        using var workbook = new XSSFWorkbook();
        var sheet = workbook.CreateSheet("Items");
        sheet.CreateRow(0).CreateCell(0, CellType.String).SetCellValue("dtgen=table, class=Item");
        sheet.CreateRow(1).CreateCell(0, CellType.String).SetCellValue("Identifier");
        sheet.CreateRow(2).CreateCell(0, CellType.String).SetCellValue("Id");
        sheet.CreateRow(3).CreateCell(0, CellType.String).SetCellValue("int");
        sheet.CreateRow(4).CreateCell(0, CellType.Numeric).SetCellValue(1);
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
