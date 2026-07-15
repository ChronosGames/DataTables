using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DataTables.GeneratorCore;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace DataTables.Tests;

public class DataTableTemplateTests
{
    [Fact]
    public void GroupedIndex_Output_Should_Compile()
    {
        var context = new GenerationContext { ClassName = "UnlockSkill" };
        context.Fields =
        [
            new XField(0) { Name = "Id", TypeName = "int" },
            new XField(1) { Name = "SkillPos", TypeName = "int" },
        ];
        context.AddIndex(["Id"]);
        context.AddGroup(["SkillPos"]);

        var code = new DataTableTemplate(context).TransformText();

        code.Should().Contain("public static IReadOnlyList<UnlockSkill>? GetManyBySkillPos(int skillPos)");
        code.Should().Contain("public static IReadOnlyList<UnlockSkill>? GetManyBySkillPos(string dataTableName, int skillPos)");
        code.Should().Contain("m_Dict1.Clear();");
        code.Should().Contain("m_Dict2.Clear();");
        Regex.IsMatch(code, @"}\r?\n\z").Should().BeTrue("generated files should end with one line break, not a blank line");
        AssertCompiles("GeneratedGroupedIndex", code);
    }

    [Fact]
    public void CompoundIndex_WithTableNameField_Should_GenerateUnambiguousNamedTableOverload()
    {
        var context = new GenerationContext { ClassName = "NamedEntry" };
        context.Fields =
        [
            new XField(0) { Name = "Id", TypeName = "int" },
            new XField(1) { Name = "DataTableName", TypeName = "string" },
        ];
        context.AddIndex(["Id", "DataTableName"]);

        var code = new DataTableTemplate(context).TransformText();

        code.Should().Contain("GetByIdAndDataTableName(string _dataTableName, int id, string dataTableName)");
        code.Should().Contain("GetCached<DTNamedEntry>(_dataTableName)");
        AssertCompiles("GeneratedCompoundNamedIndex", code);
    }

    [Fact]
    public void CustomUsing_Output_Should_Start_On_A_New_Line()
    {
        const string customUsing = "using System.Globalization;";
        var context = new GenerationContext
        {
            ClassName = "GameConfig",
            UsingStrings = [customUsing],
            Fields = [new XField(0) { Name = "Version", TypeName = "int" }],
        };
        var expected = $"using DataTables;{Environment.NewLine}{customUsing}";

        new DataTableTemplate(context).TransformText().Should().Contain(expected);
        var kvCode = new KvTableTemplate(context).TransformText();
        kvCode.Should().Contain(expected);
        kvCode.Should().Contain("GetVersion(string dataTableName)");
        kvCode.Should().Contain("GetCached<DTGameConfig>(dataTableName)");
        AssertCompiles("GeneratedKvCustomUsing", kvCode);
    }

    [Fact]
    public void GraphIndex_Output_Should_Compile()
    {
        var context = new GenerationContext { ClassName = "LevelGraph", DataSetType = "graph" };
        context.Fields =
        [
            new XField(0) { Name = "EdgeId", TypeName = "string" },
            new XField(1) { Name = "From", TypeName = "string" },
            new XField(2) { Name = "To", TypeName = "string" },
        ];
        context.AddIndex(["EdgeId"]);
        context.AddGroup(["From"]);
        context.AddGroup(["To"]);

        var code = new GraphTableTemplate(context).TransformText();

        code.Should().Contain("GetEdgeStatic(string dataTableName, string edgeId)");
        code.Should().Contain("GetGraphTableStatic(dataTableName)");
        AssertCompiles("GeneratedGraphIndex", code);
    }

    [Fact]
    public void TreeIndex_Output_Should_Compile()
    {
        var context = new GenerationContext { ClassName = "Node", DataSetType = "tree" };
        context.Fields =
        [
            new XField(0) { Name = "Id", TypeName = "string" },
            new XField(1) { Name = "ParentId", TypeName = "string" },
        ];
        context.AddIndex(["Id"]);
        context.AddGroup(["ParentId"]);

        var code = new TreeTableTemplate(context).TransformText();

        code.Should().Contain("GetChildrenStatic(string dataTableName, string id)");
        code.Should().Contain("GetCached<DTNode>(dataTableName)");
        AssertCompiles("GeneratedTreeIndex", code);
    }

    [Fact]
    public void MatrixValueType_Output_Should_Compile_Without_Warnings()
    {
        var context = new GenerationContext
        {
            ClassName = "FlagMatrix",
            DataSetType = "matrix",
            MatrixDefaultValue = "false",
            Fields =
            [
                new XField(0) { Name = "_key1", TypeName = "short" },
                new XField(1) { Name = "_key2", TypeName = "long" },
                new XField(2) { Name = "_value", TypeName = "bool" },
            ],
        };

        var code = new DataMatrixTemplate(context).TransformText();

        code.Should().Contain("GetRow(string dataTableName, short key1, long key2)");
        code.Should().Contain("GetCached<DTFlagMatrix>(dataTableName)");
        AssertCompiles("GeneratedFlagMatrix", code, allowWarnings: false);
    }

    [Fact]
    public void JsonField_Output_Should_Compile_Without_Warnings()
    {
        var context = new GenerationContext
        {
            ClassName = "JsonConfig",
            Fields = [new XField(0) { Name = "Payload", TypeName = "json<string>" }],
        };

        AssertCompiles("GeneratedJsonConfig", new DataTableTemplate(context).TransformText(), allowWarnings: false);
    }

    private static void AssertCompiles(string assemblyName, string code, bool allowWarnings = true)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(global::DataTables.DataTable<>).Assembly.Location));
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(code, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp9))],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var assembly = new MemoryStream();
        var result = compilation.Emit(assembly);
        var errors = result.Diagnostics.Where(x => x.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);

        errors.Should().BeEmpty();
        if (!allowWarnings)
        {
            var warnings = result.Diagnostics.Where(x => x.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Warning);
            warnings.Should().BeEmpty();
        }
    }
}
