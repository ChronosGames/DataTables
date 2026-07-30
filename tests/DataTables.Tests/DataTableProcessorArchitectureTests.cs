using System.IO;
using System.Linq;
using DataTables.GeneratorCore;
using FluentAssertions;
using Xunit;

namespace DataTables.Tests;

public class DataTableProcessorArchitectureTests
{
    [Fact]
    public void DataTableProcessor_Should_Remain_Orchestration_Only()
    {
        var source = File.ReadAllText(GetRepositoryPath("src", "DataTables.GeneratorCore", "DataTableProcessor.cs"));

        source.Should().NotContain("DATA_TABLE_SIGNATURE");
        source.Should().NotContain("DATA_TABLE_VERSION");
        source.Should().NotContain("throw new Exception");
        source.Should().NotContain("private bool Parse");
        source.Should().NotContain("private void Parse");
        source.Should().NotContain("private int Parse");
        source.Should().NotContain("DataSetType == \"kv\"");
        source.Should().NotContain("DataSetType == \"tree\"");
        source.Should().NotContain("DataSetType == \"graph\"");
    }

    [Fact]
    public void DataRowBinarySerializer_Should_Not_Own_Table_Semantic_Validation()
    {
        var source = File.ReadAllText(GetRepositoryPath(
            "src", "DataTables.GeneratorCore", "Serialization", "DataRowBinarySerializer.cs"));

        source.Should().NotContain("ValidateTreeRows");
        source.Should().NotContain("ValidateGraphRows");
        source.Should().NotContain("检测到循环引用");
        source.Should().NotContain("ParentId 引用不存在");
    }

    [Fact]
    public void ParserRegistry_Should_Not_Report_Supported_Types_As_Reserved()
    {
        var registry = TableSchemaParserRegistry.CreateDefault();

        registry.SupportedDataSetTypes.Should().NotIntersectWith(registry.ReservedDataSetTypes);
    }

    private static string GetRepositoryPath(params string[] parts)
    {
        var current = Directory.GetCurrentDirectory();
        while (current != null)
        {
            var candidate = Path.Combine(new[] { current }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("Unable to locate repository root for architecture test.");
    }
}
