using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DataTables.GeneratorCore;
using FluentAssertions;
using Xunit;

namespace DataTables.Tests;

public class DataProcessorStateTests
{
    [Fact]
    public void DataProcessor_Should_Not_Expose_Mutable_TypeDescriptor_State()
    {
        typeof(DataTableProcessor.DataProcessor)
            .GetProperty("TypeDescriptor", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Should().BeNull("data processors are cached and must not carry per-parse descriptor state");
    }

    [Fact]
    public void GetDataProcessor_Should_Parse_Nested_Composite_Types_Concurrently()
    {
        var utilityType = typeof(DataTableProcessor).GetNestedType("DataProcessorUtility", BindingFlags.NonPublic)!;
        var method = utilityType.GetMethod("GetDataProcessor", BindingFlags.Public | BindingFlags.Static)!;
        var types = new[]
        {
            "array<map<string,int>>",
            "map<string,array<int>>",
            "array<array<string>>",
            "json<SamplePayload>",
            "custom<ExternalType>"
        };

        var processors = Enumerable.Range(0, 256)
            .AsParallel()
            .Select(i => method.Invoke(null, new object[] { types[i % types.Length] }))
            .ToArray();

        processors.Should().OnlyContain(processor => processor != null);
    }
}
