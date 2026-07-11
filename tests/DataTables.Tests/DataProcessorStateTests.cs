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

    [Theory]
    [InlineData("array<enum<ConsoleApp.ColorT>>", "ConsoleApp.ColorT[]")]
    [InlineData("map<enum<ConsoleApp.ColorT>,int>", "Dictionary<ConsoleApp.ColorT, int>")]
    [InlineData("array<custom<ConsoleApp.Payload>>", "ConsoleApp.Payload[]")]
    public void GetLanguageKeyword_Should_Preserve_UserDefined_TypeCase_In_CompositeTypes(string typeName, string expected)
    {
        DataTableProcessor.GetLanguageKeyword(new XField(0) { TypeName = typeName }).Should().Be(expected);
    }

    [Theory]
    [InlineData("map<string,int>", "{\"b\":2,\"a\":1}", "{\"a\":1,\"b\":2}")]
    [InlineData("map<int,string>", "{\"10\":\"ten\",\"2\":\"two\"}", "{\"2\":\"two\",\"10\":\"ten\"}")]
    public void MapSerialization_Should_Be_Deterministic(string typeName, string firstValue, string secondValue)
    {
        var utilityType = typeof(DataTableProcessor).GetNestedType("DataProcessorUtility", BindingFlags.NonPublic)!;
        var method = utilityType.GetMethod("GetDataProcessor", BindingFlags.Public | BindingFlags.Static)!;
        var processor = (DataTableProcessor.DataProcessor)method.Invoke(null, new object[] { typeName })!;

        Serialize(processor, firstValue).Should().Equal(Serialize(processor, secondValue));
    }

    private static byte[] Serialize(DataTableProcessor.DataProcessor processor, string value)
    {
        using var stream = new System.IO.MemoryStream();
        using var writer = new System.IO.BinaryWriter(stream);
        processor.WriteToStream(writer, value);
        return stream.ToArray();
    }
}
