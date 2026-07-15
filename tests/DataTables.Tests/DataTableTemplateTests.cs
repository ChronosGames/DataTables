using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
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

        code.Should().Contain("public static IReadOnlyList<UnlockSkill>? GetManyBySkillPos(DataTableContext context, int skillPos)");
        code.Should().Contain("public static IReadOnlyList<UnlockSkill>? GetManyBySkillPos(DataTableContext context, string dataTableName, int skillPos)");
        code.Should().NotContain("DataTableManager.GetCached");
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

        code.Should().Contain("GetByIdAndDataTableName(DataTableContext context, string _dataTableName, int id, string dataTableName)");
        code.Should().Contain("context.GetCached<DTNamedEntry>(_dataTableName)");
        AssertCompiles("GeneratedCompoundNamedIndex", code);
    }

    [Fact]
    public void Index_WithContextField_Should_GenerateUnambiguousContextParameter()
    {
        var context = new GenerationContext { ClassName = "ContextEntry" };
        context.Fields = [new XField(0) { Name = "Context", TypeName = "string" }];
        context.AddIndex(["Context"]);

        var code = new DataTableTemplate(context).TransformText();

        code.Should().Contain("GetByContext(DataTableContext _context, string context)");
        code.Should().Contain("_context.GetCached<DTContextEntry>(dataTableName)");
        AssertCompiles("GeneratedContextNamedIndex", code);
    }

    [Fact]
    public async Task GeneratedQueries_ShouldReadOnlyFromTheSuppliedDefaultAndNamedContexts()
    {
        var generationContext = new GenerationContext
        {
            Namespace = "DataTables.Tests.Dynamic",
            ClassName = "ContextItem",
            Fields =
            [
                new XField(0) { Name = "Id", TypeName = "int" },
                new XField(1) { Name = "Value", TypeName = "string" },
            ],
        };
        generationContext.AddIndex(["Id"]);
        var assembly = CompileAndLoad("GeneratedContextQueries_" + Guid.NewGuid().ToString("N"), new DataTableTemplate(generationContext).TransformText());
        var tableType = assembly.GetType("DataTables.Tests.Dynamic.DTContextItem", throwOnError: true)!;
        var schemaHash = (ulong)tableType.GetProperty(nameof(global::DataTables.DataTableBase.SchemaHash))!
            .GetValue(Activator.CreateInstance(tableType, string.Empty, 0))!;
        var tableName = tableType.FullName!;
        using var first = new global::DataTables.DataTableContext(new DictionaryDataSource(new Dictionary<string, byte[]>
        {
            [tableName] = CreateTablePayload(tableName, schemaHash, 1, "first-default"),
            [tableName + ".named"] = CreateTablePayload(tableName, schemaHash, 1, "first-named"),
        }));
        using var second = new global::DataTables.DataTableContext(new DictionaryDataSource(new Dictionary<string, byte[]>
        {
            [tableName] = CreateTablePayload(tableName, schemaHash, 1, "second-default"),
            [tableName + ".named"] = CreateTablePayload(tableName, schemaHash, 1, "second-named"),
        }));
        await LoadDynamicTableAsync(first, tableType, string.Empty);
        await LoadDynamicTableAsync(first, tableType, "named");
        await LoadDynamicTableAsync(second, tableType, string.Empty);
        await LoadDynamicTableAsync(second, tableType, "named");

        ReadValue(tableType, first, null).Should().Be("first-default");
        ReadValue(tableType, first, "named").Should().Be("first-named");
        ReadValue(tableType, second, null).Should().Be("second-default");
        ReadValue(tableType, second, "named").Should().Be("second-named");
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
        kvCode.Should().Contain("GetVersion(DataTableContext context, string dataTableName)");
        kvCode.Should().Contain("context.GetCached<DTGameConfig>(dataTableName)");
        kvCode.Should().NotContain("public static int Version");
        kvCode.Should().NotContain("DataTableManager.");
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

        code.Should().Contain("GetEdge(DataTableContext context, string dataTableName, string edgeId)");
        code.Should().Contain("GetGraphTable(context, dataTableName)");
        code.Should().NotContain("GetEdgeStatic");
        code.Should().NotContain("DataTableManager.");
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

        code.Should().Contain("GetChildren(DataTableContext context, string dataTableName, string id)");
        code.Should().Contain("context.GetCached<DTNode>(dataTableName)");
        code.Should().NotContain("GetChildrenStatic");
        code.Should().NotContain("DataTableManager.");
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

        code.Should().Contain("GetRow(DataTableContext context, string dataTableName, short key1, long key2)");
        code.Should().Contain("IsLoaded(DataTableContext context, string dataTableName)");
        code.Should().Contain("context.GetCached<DTFlagMatrix>(dataTableName)");
        code.Should().NotContain("DataTableManager.GetCached");
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

    [Fact]
    public void GeneratedManagerPreload_ShouldReturnDetailedPreheatResult()
    {
        var template = new DataTableManagerExtensionTemplate
        {
            Namespace = "GeneratedManager",
            DataTables = new Dictionary<string, IOrderedEnumerable<string>>()
                .OrderBy(pair => pair.Key, StringComparer.Ordinal),
            TablePriorities = new Dictionary<string, string>(),
        };

        var code = template.TransformText();

        code.Should().Contain("ValueTask<PreheatResult> PreloadAsync(DataTableContext context, Priority priorities = Priority.All");
        code.Should().Contain("ValueTask<PreheatResult> PreloadAsync(DataTableContext context, Priority priorities, PreheatOptions options");
        code.Should().Contain("public static void Register()");
        AssertCompiles("GeneratedManagerPreload", code, allowWarnings: false);
    }

    private static void AssertCompiles(string assemblyName, string code, bool allowWarnings = true)
    {
        using var assembly = Compile(assemblyName, code, allowWarnings);
    }

    private static Assembly CompileAndLoad(string assemblyName, string code)
    {
        using var assembly = Compile(assemblyName, code, allowWarnings: true);
        return Assembly.Load(assembly.ToArray());
    }

    private static MemoryStream Compile(string assemblyName, string code, bool allowWarnings)
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

        var assembly = new MemoryStream();
        var result = compilation.Emit(assembly);
        var errors = result.Diagnostics.Where(x => x.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);

        errors.Should().BeEmpty();
        if (!allowWarnings)
        {
            var warnings = result.Diagnostics.Where(x => x.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Warning);
            warnings.Should().BeEmpty();
        }

        assembly.Position = 0;
        return assembly;
    }

    private static byte[] CreateTablePayload(string tableName, ulong schemaHash, int id, string value)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("DTABLE");
        writer.Write(3);
        writer.Write(schemaHash);
        writer.Write("tests");
        writer.Write(tableName);
        writer.Write((ushort)1);
        writer.Write(0);
        global::DataTables.BinaryExtension.Write7BitEncodedInt32(writer, id);
        writer.Write(value);
        return stream.ToArray();
    }

    private static async Task LoadDynamicTableAsync(global::DataTables.DataTableContext context, Type tableType, string name)
    {
        var loadMethod = typeof(global::DataTables.DataTableContext).GetMethods()
            .Single(method => method.Name == nameof(global::DataTables.DataTableContext.LoadAsync)
                && method.IsGenericMethodDefinition
                && method.GetParameters().Length == 2)
            .MakeGenericMethod(tableType);
        var valueTask = loadMethod.Invoke(context, [name, CancellationToken.None])!;
        var task = (Task)valueTask.GetType().GetMethod("AsTask")!.Invoke(valueTask, null)!;
        await task;
    }

    private static string ReadValue(Type tableType, global::DataTables.DataTableContext context, string? name)
    {
        var parameterTypes = name == null
            ? new[] { typeof(global::DataTables.DataTableContext), typeof(int) }
            : new[] { typeof(global::DataTables.DataTableContext), typeof(string), typeof(int) };
        var method = tableType.GetMethod("GetById", BindingFlags.Public | BindingFlags.Static, null, parameterTypes, null)!;
        var row = name == null
            ? method.Invoke(null, [context, 1])
            : method.Invoke(null, [context, name, 1]);
        return (string)row!.GetType().GetProperty("Value")!.GetValue(row)!;
    }

    private sealed class DictionaryDataSource : global::DataTables.IDataSource
    {
        private readonly IReadOnlyDictionary<string, byte[]> m_Payloads;

        public DictionaryDataSource(IReadOnlyDictionary<string, byte[]> payloads)
        {
            m_Payloads = payloads;
        }

        public global::DataTables.DataSourceType SourceType => global::DataTables.DataSourceType.Memory;

        public ValueTask<Stream> OpenReadAsync(string name, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<Stream>(new MemoryStream(m_Payloads[name], writable: false));
        }

        public ValueTask<bool> ExistsAsync(string name, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(m_Payloads.ContainsKey(name));
        }

        public ValueTask<global::DataTables.DataSourceManifest> GetManifestAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(global::DataTables.DataSourceManifest.Empty);
        }

        public ValueTask<bool> IsAvailableAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(true);
        }
    }
}
