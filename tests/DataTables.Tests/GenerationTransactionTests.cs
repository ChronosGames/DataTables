using System;
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
        var codeOutput = Path.Combine(code, "DRItem.cs");
        var managerOutput = Path.Combine(code, "DataTableManagerExtension.cs");
        var output = Path.Combine(data, "DataTables.Tests.Generated.DTItem.bytes");
        var manifest = Path.Combine(data, ".dtgen-manifest.json");
        var codeText = await File.ReadAllTextAsync(codeOutput);
        var managerText = await File.ReadAllTextAsync(managerOutput);
        var outputBytes = await File.ReadAllBytesAsync(output);
        var manifestText = await File.ReadAllTextAsync(manifest);

        await File.WriteAllTextAsync(workbook, "not an Excel workbook");
        var result = await GenerateAsync(input, code, data, forceOverwrite: false);

        result.Succeeded.Should().BeFalse();
        (await File.ReadAllTextAsync(codeOutput)).Should().Be(codeText);
        (await File.ReadAllTextAsync(managerOutput)).Should().Be(managerText);
        (await File.ReadAllBytesAsync(output)).Should().Equal(outputBytes);
        (await File.ReadAllTextAsync(manifest)).Should().Be(manifestText);
        Directory.EnumerateDirectories(code, ".dtgen-*", SearchOption.TopDirectoryOnly).Should().BeEmpty();
        Directory.EnumerateDirectories(data, ".dtgen-*", SearchOption.TopDirectoryOnly).Should().BeEmpty();
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DataOnlyGeneration_ShouldPreserveCodeManagerAndCodeManifest(bool useExplicitMode)
    {
        var root = CreateTempDirectory();
        var input = Directory.CreateDirectory(Path.Combine(root, "input")).FullName;
        var code = Directory.CreateDirectory(Path.Combine(root, "code")).FullName;
        var data = Directory.CreateDirectory(Path.Combine(root, "data")).FullName;
        var workbook = Path.Combine(input, "items.xlsx");
        await CreateValidWorkbookAsync(workbook, id: 1);
        (await GenerateAsync(input, code, data)).Succeeded.Should().BeTrue();
        var rowCode = Path.Combine(code, "DRItem.cs");
        var managerCode = Path.Combine(code, "DataTableManagerExtension.cs");
        var codeManifest = Path.Combine(data, ".dtgen-manifest.json");
        var dataFile = Path.Combine(data, "DataTables.Tests.Generated.DTItem.bytes");
        var rowCodeText = await File.ReadAllTextAsync(rowCode);
        var managerCodeText = await File.ReadAllTextAsync(managerCode);
        var codeManifestText = await File.ReadAllTextAsync(codeManifest);
        var previousData = await File.ReadAllBytesAsync(dataFile);

        await CreateValidWorkbookAsync(workbook, id: 2);
        var result = await GenerateAsync(
            input,
            string.Empty,
            data,
            generationMode: useExplicitMode ? GenerationMode.DataOnly : null);

        result.Succeeded.Should().BeTrue();
        (await File.ReadAllTextAsync(rowCode)).Should().Be(rowCodeText);
        (await File.ReadAllTextAsync(managerCode)).Should().Be(managerCodeText);
        (await File.ReadAllTextAsync(codeManifest)).Should().Be(codeManifestText);
        (await File.ReadAllBytesAsync(dataFile)).Should().NotEqual(previousData);
        File.Exists(Path.Combine(data, ".dtgen-data-manifest.json")).Should().BeTrue();
        Directory.EnumerateDirectories(code, ".dtgen-*", SearchOption.TopDirectoryOnly).Should().BeEmpty();
        Directory.EnumerateDirectories(data, ".dtgen-*", SearchOption.TopDirectoryOnly).Should().BeEmpty();
    }


    [Fact]
    public async Task ValidateOnlyGeneration_ShouldParseAndRenderWithoutWritingOutputs()
    {
        var root = CreateTempDirectory();
        var input = Directory.CreateDirectory(Path.Combine(root, "input")).FullName;
        var code = Directory.CreateDirectory(Path.Combine(root, "code")).FullName;
        var data = Directory.CreateDirectory(Path.Combine(root, "data")).FullName;
        await CreateValidWorkbookAsync(Path.Combine(input, "items.xlsx"));
        var logs = new System.Collections.Generic.List<string>();

        var result = await GenerateAsync(
            input,
            code,
            data,
            generationMode: GenerationMode.ValidateOnly,
            logger: logs.Add);

        result.Succeeded.Should().BeTrue();
        result.SucceededCount.Should().Be(1);
        Directory.GetFiles(code, "*", SearchOption.AllDirectories).Should().BeEmpty();
        Directory.GetFiles(data, "*", SearchOption.AllDirectories).Should().BeEmpty();
        Directory.EnumerateDirectories(code, ".dtgen-*", SearchOption.TopDirectoryOnly).Should().BeEmpty();
        Directory.EnumerateDirectories(data, ".dtgen-*", SearchOption.TopDirectoryOnly).Should().BeEmpty();
        logs.Should().Contain(message => message.Contains("数据表校验完成"));
    }

    [Fact]
    public async Task ValidateOnlyGeneration_ShouldReportTemplateAndSchemaConflictsWithoutTouchingOutputs()
    {
        var root = CreateTempDirectory();
        var input = Directory.CreateDirectory(Path.Combine(root, "input")).FullName;
        var code = Directory.CreateDirectory(Path.Combine(root, "code")).FullName;
        var data = Directory.CreateDirectory(Path.Combine(root, "data")).FullName;
        await CreateValidWorkbookAsync(Path.Combine(input, "items-a.xlsx"), id: 1);
        await CreateValidWorkbookAsync(Path.Combine(input, "items-b.xlsx"), id: 2);

        var result = await GenerateAsync(input, code, data, generationMode: GenerationMode.ValidateOnly);

        result.Succeeded.Should().BeFalse();
        result.Failures.Should().ContainSingle(x =>
            x.Exception.Message.Contains("Generated data output conflict")
            && x.Exception.Message.Contains("DataTables.Tests.Generated.DTItem.bytes"));
        Directory.GetFiles(code, "*", SearchOption.AllDirectories).Should().BeEmpty();
        Directory.GetFiles(data, "*", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Fact]
    public async Task InvalidGenerationMode_ShouldRejectWithoutTouchingExistingOutputs()
    {
        var root = CreateTempDirectory();
        var input = Directory.CreateDirectory(Path.Combine(root, "input")).FullName;
        var code = Directory.CreateDirectory(Path.Combine(root, "code")).FullName;
        var data = Directory.CreateDirectory(Path.Combine(root, "data")).FullName;
        await CreateValidWorkbookAsync(Path.Combine(input, "items.xlsx"));
        (await GenerateAsync(input, code, data)).Succeeded.Should().BeTrue();
        var rowCode = Path.Combine(code, "DRItem.cs");
        var managerCode = Path.Combine(code, "DataTableManagerExtension.cs");
        var dataFile = Path.Combine(data, "DataTables.Tests.Generated.DTItem.bytes");
        var manifest = Path.Combine(data, ".dtgen-manifest.json");
        var rowCodeText = await File.ReadAllTextAsync(rowCode);
        var managerCodeText = await File.ReadAllTextAsync(managerCode);
        var dataBytes = await File.ReadAllBytesAsync(dataFile);
        var manifestText = await File.ReadAllTextAsync(manifest);

        Func<Task> action = () => GenerateAsync(input, code, data, generationMode: (GenerationMode)int.MaxValue);

        await action.Should().ThrowAsync<ArgumentOutOfRangeException>();
        (await File.ReadAllTextAsync(rowCode)).Should().Be(rowCodeText);
        (await File.ReadAllTextAsync(managerCode)).Should().Be(managerCodeText);
        (await File.ReadAllBytesAsync(dataFile)).Should().Equal(dataBytes);
        (await File.ReadAllTextAsync(manifest)).Should().Be(manifestText);
        File.Exists(Path.Combine(data, ".dtgen-data-manifest.json")).Should().BeFalse();
    }

    [Fact]
    public async Task ErrorDiagnostic_ShouldFailAndKeepDiagnosticsReportAvailable()
    {
        var root = CreateTempDirectory();
        var input = Directory.CreateDirectory(Path.Combine(root, "input")).FullName;
        var code = Directory.CreateDirectory(Path.Combine(root, "code")).FullName;
        var data = Directory.CreateDirectory(Path.Combine(root, "data")).FullName;
        var reportPath = Path.Combine(root, "diagnostics", "report.json");
        await CreateValidWorkbookAsync(Path.Combine(input, "items.xlsx"));
        (await GenerateAsync(input, code, data)).Succeeded.Should().BeTrue();
        var rowCode = Path.Combine(code, "DRItem.cs");
        var managerCode = Path.Combine(code, "DataTableManagerExtension.cs");
        var dataFile = Path.Combine(data, "DataTables.Tests.Generated.DTItem.bytes");
        var manifest = Path.Combine(data, ".dtgen-manifest.json");
        var rowCodeText = await File.ReadAllTextAsync(rowCode);
        var managerCodeText = await File.ReadAllTextAsync(managerCode);
        var dataBytes = await File.ReadAllBytesAsync(dataFile);
        var manifestText = await File.ReadAllTextAsync(manifest);
        await CreateUnknownTableWorkbookAsync(Path.Combine(input, "unknown.xlsx"));

        var result = await GenerateAsync(input, code, data, forceOverwrite: false, diagnosticsJsonOutput: reportPath);

        result.Succeeded.Should().BeFalse();
        result.Diagnostics.Should().ContainSingle(x => x.Severity == DiagnosticSeverity.Error);
        result.Failures.Should().ContainSingle(x => x.Exception.Message.Contains("Generator diagnostic error"));
        File.Exists(reportPath).Should().BeTrue();
        using var report = JsonDocument.Parse(await File.ReadAllTextAsync(reportPath));
        report.RootElement.GetProperty("ErrorCount").GetInt32().Should().Be(1);
        (await File.ReadAllTextAsync(rowCode)).Should().Be(rowCodeText);
        (await File.ReadAllTextAsync(managerCode)).Should().Be(managerCodeText);
        (await File.ReadAllBytesAsync(dataFile)).Should().Equal(dataBytes);
        (await File.ReadAllTextAsync(manifest)).Should().Be(manifestText);
        Directory.EnumerateDirectories(code, ".dtgen-*", SearchOption.TopDirectoryOnly).Should().BeEmpty();
        Directory.EnumerateDirectories(data, ".dtgen-*", SearchOption.TopDirectoryOnly).Should().BeEmpty();
    }

    [Fact]
    public async Task EquivalentSplitWorkbooks_ShouldShareCodeOutputWithoutRacing()
    {
        var root = CreateTempDirectory();
        var input = Directory.CreateDirectory(Path.Combine(root, "input")).FullName;
        var code = Directory.CreateDirectory(Path.Combine(root, "code")).FullName;
        var data = Directory.CreateDirectory(Path.Combine(root, "data")).FullName;
        await CreateWorkbookAsync(Path.Combine(input, "split-1.xlsx"), workbook =>
        {
            AddTableSheet(workbook, "Split1", "SplitItem", 1, child: "x001");
        });
        await CreateWorkbookAsync(Path.Combine(input, "split-2.xlsx"), workbook =>
        {
            AddTableSheet(workbook, "Split2", "SplitItem", 2, child: "x002");
        });

        var result = await GenerateAsync(input, code, data);
        var incrementalResult = await GenerateAsync(input, code, data, forceOverwrite: false);

        result.Succeeded.Should().BeTrue();
        incrementalResult.Succeeded.Should().BeTrue();
        incrementalResult.SkippedCount.Should().Be(2);
        Directory.GetFiles(code, "DRSplitItem.cs").Should().ContainSingle();
        Directory.GetFiles(data, "DataTables.Tests.Generated.DTSplitItem.*.bytes").Select(Path.GetFileName)
            .Should().BeEquivalentTo(
                "DataTables.Tests.Generated.DTSplitItem.x001.bytes",
                "DataTables.Tests.Generated.DTSplitItem.x002.bytes");
    }

    [Fact]
    public async Task SplitSheetsWithDifferentCode_ShouldFailInsteadOfOverwriting()
    {
        var root = CreateTempDirectory();
        var input = Directory.CreateDirectory(Path.Combine(root, "input")).FullName;
        var code = Directory.CreateDirectory(Path.Combine(root, "code")).FullName;
        var data = Directory.CreateDirectory(Path.Combine(root, "data")).FullName;
        var workbookPath = Path.Combine(input, "split.xlsx");
        await CreateWorkbookAsync(workbookPath, workbook =>
        {
            AddTableSheet(workbook, "Split1", "SplitItem", 1, child: "x001");
        });
        (await GenerateAsync(input, code, data)).Succeeded.Should().BeTrue();
        var rowCode = Path.Combine(code, "DRSplitItem.cs");
        var managerCode = Path.Combine(code, "DataTableManagerExtension.cs");
        var dataFile = Path.Combine(data, "DataTables.Tests.Generated.DTSplitItem.x001.bytes");
        var manifest = Path.Combine(data, ".dtgen-manifest.json");
        var rowCodeText = await File.ReadAllTextAsync(rowCode);
        var managerCodeText = await File.ReadAllTextAsync(managerCode);
        var dataBytes = await File.ReadAllBytesAsync(dataFile);
        var manifestText = await File.ReadAllTextAsync(manifest);
        await CreateWorkbookAsync(workbookPath, workbook =>
        {
            AddTableSheet(workbook, "Split1", "SplitItem", 1, child: "x001");
            AddTableSheet(workbook, "Split2", "SplitItem", 2, child: "x002", includeName: true);
        });

        var result = await GenerateAsync(input, code, data, forceOverwrite: false);

        result.Succeeded.Should().BeFalse();
        result.Failures.Should().ContainSingle(x =>
            x.Exception.Message.Contains("Generated code output conflict")
            && x.Exception.Message.Contains("DRSplitItem.cs"));
        (await File.ReadAllTextAsync(rowCode)).Should().Be(rowCodeText);
        (await File.ReadAllTextAsync(managerCode)).Should().Be(managerCodeText);
        (await File.ReadAllBytesAsync(dataFile)).Should().Equal(dataBytes);
        (await File.ReadAllTextAsync(manifest)).Should().Be(manifestText);
        File.Exists(Path.Combine(data, "DataTables.Tests.Generated.DTSplitItem.x002.bytes")).Should().BeFalse();
        Directory.EnumerateDirectories(code, ".dtgen-*", SearchOption.TopDirectoryOnly).Should().BeEmpty();
        Directory.EnumerateDirectories(data, ".dtgen-*", SearchOption.TopDirectoryOnly).Should().BeEmpty();
    }

    [Fact]
    public async Task DuplicateDataOutputAcrossWorkbooks_ShouldFailInsteadOfOverwriting()
    {
        var root = CreateTempDirectory();
        var input = Directory.CreateDirectory(Path.Combine(root, "input")).FullName;
        var code = Directory.CreateDirectory(Path.Combine(root, "code")).FullName;
        var data = Directory.CreateDirectory(Path.Combine(root, "data")).FullName;
        await CreateValidWorkbookAsync(Path.Combine(input, "items-a.xlsx"), id: 1);
        (await GenerateAsync(input, code, data)).Succeeded.Should().BeTrue();
        var rowCode = Path.Combine(code, "DRItem.cs");
        var managerCode = Path.Combine(code, "DataTableManagerExtension.cs");
        var dataFile = Path.Combine(data, "DataTables.Tests.Generated.DTItem.bytes");
        var manifest = Path.Combine(data, ".dtgen-manifest.json");
        var rowCodeText = await File.ReadAllTextAsync(rowCode);
        var managerCodeText = await File.ReadAllTextAsync(managerCode);
        var dataBytes = await File.ReadAllBytesAsync(dataFile);
        var manifestText = await File.ReadAllTextAsync(manifest);
        await CreateValidWorkbookAsync(Path.Combine(input, "items-b.xlsx"), id: 2);

        var result = await GenerateAsync(input, code, data, forceOverwrite: false);

        result.Succeeded.Should().BeFalse();
        result.Failures.Should().ContainSingle(x =>
            x.Exception.Message.Contains("Generated data output conflict")
            && x.Exception.Message.Contains("DataTables.Tests.Generated.DTItem.bytes"));
        (await File.ReadAllTextAsync(rowCode)).Should().Be(rowCodeText);
        (await File.ReadAllTextAsync(managerCode)).Should().Be(managerCodeText);
        (await File.ReadAllBytesAsync(dataFile)).Should().Equal(dataBytes);
        (await File.ReadAllTextAsync(manifest)).Should().Be(manifestText);
        Directory.EnumerateDirectories(code, ".dtgen-*", SearchOption.TopDirectoryOnly).Should().BeEmpty();
        Directory.EnumerateDirectories(data, ".dtgen-*", SearchOption.TopDirectoryOnly).Should().BeEmpty();
    }

    private static Task<GenerationResult> GenerateAsync(string input, string code, string data, bool forceOverwrite = true, Action<string>? logger = null, string dataRowClassPrefix = "DR", string? diagnosticsJsonOutput = null, GenerationMode? generationMode = null)
    {
        var generator = new DataTableGenerator();
        if (generationMode.HasValue)
        {
            return generator.GenerateFile(
                inputDirectories: new[] { input },
                searchPatterns: new[] { "*.xlsx" },
                codeOutputDir: code,
                dataOutputDir: data,
                usingNamespace: "DataTables.Tests.Generated",
                dataRowClassPrefix: dataRowClassPrefix,
                importNamespaces: string.Empty,
                filterColumnTags: string.Empty,
                forceOverwrite: forceOverwrite,
                logger: logger ?? (_ => { }),
                generationMode: generationMode.Value,
                diagnosticsJsonOutput: diagnosticsJsonOutput);
        }

        return generator.GenerateFile(
            inputDirectories: new[] { input },
            searchPatterns: new[] { "*.xlsx" },
            codeOutputDir: code,
            dataOutputDir: data,
            usingNamespace: "DataTables.Tests.Generated",
            dataRowClassPrefix: dataRowClassPrefix,
            importNamespaces: string.Empty,
            filterColumnTags: string.Empty,
            forceOverwrite: forceOverwrite,
            logger: logger ?? (_ => { }),
            diagnosticsJsonOutput: diagnosticsJsonOutput);
    }

    private static async Task CreateValidWorkbookAsync(string path, string className = "Item", int id = 1)
    {
        await CreateWorkbookAsync(path, workbook => AddTableSheet(workbook, "Items", className, id));
    }

    private static async Task CreateUnknownTableWorkbookAsync(string path)
    {
        await CreateWorkbookAsync(path, workbook =>
        {
            var sheet = workbook.CreateSheet("Unknown");
            sheet.CreateRow(0).CreateCell(0, CellType.String).SetCellValue("dtgen=unknown, class=UnknownItem");
        });
    }

    private static async Task CreateWorkbookAsync(string path, Action<XSSFWorkbook> configure)
    {
        using var workbook = new XSSFWorkbook();
        configure(workbook);
        await using var stream = File.Create(path);
        workbook.Write(stream);
    }

    private static void AddTableSheet(XSSFWorkbook workbook, string sheetName, string className, int id, string child = "", bool includeName = false)
    {
        var sheet = workbook.CreateSheet(sheetName);
        var childSetting = string.IsNullOrEmpty(child) ? string.Empty : $", child={child}";
        sheet.CreateRow(0).CreateCell(0, CellType.String).SetCellValue($"dtgen=table, class={className}{childSetting}");
        sheet.CreateRow(1).CreateCell(0, CellType.String).SetCellValue("Identifier");
        sheet.CreateRow(2).CreateCell(0, CellType.String).SetCellValue("Id");
        sheet.CreateRow(3).CreateCell(0, CellType.String).SetCellValue("int");
        sheet.CreateRow(4).CreateCell(0, CellType.Numeric).SetCellValue(id);
        if (!includeName)
        {
            return;
        }

        sheet.GetRow(1).CreateCell(1, CellType.String).SetCellValue("Name");
        sheet.GetRow(2).CreateCell(1, CellType.String).SetCellValue("Name");
        sheet.GetRow(3).CreateCell(1, CellType.String).SetCellValue("string");
        sheet.GetRow(4).CreateCell(1, CellType.String).SetCellValue("Item");
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "dt_generation_transaction_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
