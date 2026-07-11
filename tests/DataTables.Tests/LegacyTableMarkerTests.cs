using System;
using System.IO;
using System.Threading.Tasks;
using DataTables.GeneratorCore;
using FluentAssertions;
using NPOI.XSSF.UserModel;
using Xunit;

namespace DataTables.Tests;

public class LegacyTableMarkerTests
{
    [Theory]
    [InlineData("DataTabeGenerator")]
    [InlineData("DataTableGenerator")]
    public void CreateGenerationContext_Should_Support_Legacy_TableMarker(string marker)
    {
        using var workbook = new XSSFWorkbook();
        var sheet = CreateTableSheet(workbook, marker);
        var context = new GenerationContext { FileName = "legacy.xlsx", SheetName = sheet.SheetName };
        using var processor = new DataTableProcessor(context, null!, new ParseOptions(), new DiagnosticsCollector());

        processor.CreateGenerationContext(sheet);

        context.DataSetType.Should().Be("table");
        processor.ValidateGenerationContext().Should().BeTrue();
    }

    [Theory]
    [InlineData("DataTabeGenerator")]
    [InlineData("DataTableGenerator")]
    public async Task GenerateFile_Should_Support_Legacy_TableMarker(string marker)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "dt_legacy_marker_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var codeOutputDir = Path.Combine(tempDir, "code");
        var dataOutputDir = Path.Combine(tempDir, "data");
        var workbookPath = Path.Combine(tempDir, "legacy.xlsx");

        using (var workbook = new XSSFWorkbook())
        {
            CreateTableSheet(workbook, marker);
            await using var stream = File.Create(workbookPath);
            workbook.Write(stream);
        }

        var result = await new DataTableGenerator().GenerateFile(
            inputDirectories: new[] { tempDir },
            searchPatterns: new[] { "*.xlsx" },
            codeOutputDir: codeOutputDir,
            dataOutputDir: dataOutputDir,
            usingNamespace: "DataTables.Tests.Generated",
            dataRowClassPrefix: string.Empty,
            importNamespaces: string.Empty,
            filterColumnTags: string.Empty,
            forceOverwrite: true,
            logger: _ => { });

        result.Succeeded.Should().BeTrue();
        File.Exists(Path.Combine(dataOutputDir, "DataTables.Tests.Generated.DTLegacy.bytes")).Should().BeTrue();
    }

    private static NPOI.SS.UserModel.ISheet CreateTableSheet(XSSFWorkbook workbook, string marker)
    {
        var sheet = workbook.CreateSheet("Legacy");
        sheet.CreateRow(0).CreateCell(0).SetCellValue(marker + ",class=Legacy");
        sheet.CreateRow(1).CreateCell(0).SetCellValue("Id");
        sheet.CreateRow(2).CreateCell(0).SetCellValue("Id");
        sheet.CreateRow(3).CreateCell(0).SetCellValue("int");
        sheet.CreateRow(4).CreateCell(0).SetCellValue("1");
        return sheet;
    }
}
