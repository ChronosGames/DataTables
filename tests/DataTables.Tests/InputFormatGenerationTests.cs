using System;
using System.IO;
using System.Threading.Tasks;
using DataTables.GeneratorCore;
using FluentAssertions;
using NPOI.XSSF.UserModel;
using Xunit;

namespace DataTables.Tests;

public class InputFormatGenerationTests
{
    [Fact]
    public async Task CsvInput_ShouldGenerateCodeAndData()
    {
        using var paths = TestPaths.Create();
        var csvPath = Path.Combine(paths.Input, "items.csv");
        await File.WriteAllTextAsync(csvPath, string.Join("\n", new[]
        {
            "DTGen=table;Class=Item",
            "Identifier,Display Name",
            "Id,Name",
            "int,string",
            "1,\"Potion, Small\"",
        }));

        var result = await GenerateAsync(paths, ["*.csv"]);

        result.Succeeded.Should().BeTrue();
        File.Exists(Path.Combine(paths.Code, "DRItem.cs")).Should().BeTrue();
        File.Exists(Path.Combine(paths.Data, "DRItem.bytes")).Should().BeTrue();
    }

    [Fact]
    public async Task XlsmInput_ShouldGenerateCodeAndData()
    {
        using var paths = TestPaths.Create();
        var xlsmPath = Path.Combine(paths.Input, "items.xlsm");
        await CreateWorkbookAsync(xlsmPath);

        var result = await GenerateAsync(paths, ["*.xlsm"]);

        result.Succeeded.Should().BeTrue();
        File.Exists(Path.Combine(paths.Code, "DRItem.cs")).Should().BeTrue();
        File.Exists(Path.Combine(paths.Data, "DRItem.bytes")).Should().BeTrue();
    }

    private static Task<GenerationResult> GenerateAsync(TestPaths paths, string[] searchPatterns)
    {
        return new DataTableGenerator().GenerateFile(
            inputDirectories: [paths.Input],
            searchPatterns: searchPatterns,
            codeOutputDir: paths.Code,
            dataOutputDir: paths.Data,
            usingNamespace: "Tests",
            dataRowClassPrefix: "DR",
            importNamespaces: string.Empty,
            filterColumnTags: string.Empty,
            forceOverwrite: true,
            logger: _ => { });
    }

    private static async Task CreateWorkbookAsync(string path)
    {
        using var workbook = new XSSFWorkbook();
        var sheet = workbook.CreateSheet("Items");
        sheet.CreateRow(0).CreateCell(0).SetCellValue("DTGen=table;Class=Item");
        var title = sheet.CreateRow(1);
        title.CreateCell(0).SetCellValue("Identifier");
        title.CreateCell(1).SetCellValue("Name");
        var fields = sheet.CreateRow(2);
        fields.CreateCell(0).SetCellValue("Id");
        fields.CreateCell(1).SetCellValue("Name");
        var types = sheet.CreateRow(3);
        types.CreateCell(0).SetCellValue("int");
        types.CreateCell(1).SetCellValue("string");
        var data = sheet.CreateRow(4);
        data.CreateCell(0).SetCellValue("1");
        data.CreateCell(1).SetCellValue("Potion");
        await using var stream = File.Create(path);
        workbook.Write(stream, leaveOpen: false);
    }

    private sealed class TestPaths : IDisposable
    {
        private readonly string m_Root;

        private TestPaths(string root)
        {
            m_Root = root;
            Input = Path.Combine(root, "input");
            Code = Path.Combine(root, "code");
            Data = Path.Combine(root, "data");
            Directory.CreateDirectory(Input);
            Directory.CreateDirectory(Code);
            Directory.CreateDirectory(Data);
        }

        public string Input { get; }
        public string Code { get; }
        public string Data { get; }

        public static TestPaths Create() => new(Path.Combine(Path.GetTempPath(), "DataTablesInputFormats_" + Guid.NewGuid().ToString("N")));

        public void Dispose()
        {
            if (Directory.Exists(m_Root))
            {
                Directory.Delete(m_Root, recursive: true);
            }
        }
    }
}
