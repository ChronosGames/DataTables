using System;
using System.IO;
using System.Linq;
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
        AssertCompiles("GeneratedGroupedIndex", code);
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

        AssertCompiles("GeneratedGraphIndex", new GraphTableTemplate(context).TransformText());
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

        AssertCompiles("GeneratedTreeIndex", new TreeTableTemplate(context).TransformText());
    }

    private static void AssertCompiles(string assemblyName, string code)
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
    }
}
