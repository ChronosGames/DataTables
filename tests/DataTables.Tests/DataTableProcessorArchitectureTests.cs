using System.IO;
using System.Linq;
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
