using System;
using System.IO;
using System.Reflection;
using DataTables.GeneratorCore;
using FluentAssertions;
using Xunit;

namespace DataTables.Tests;

/// <summary>
/// 验证 <see cref="ParseOptions.ArrayNestedSeparators"/> 配置下，嵌套数组按层级分隔符解析的行为。
/// 同时保证不配置时维持原有兼容行为。
/// </summary>
public class ArrayNestedSeparatorsTests : IDisposable
{
    private readonly FieldInfo m_NestedSeparatorsField;
    private readonly object? m_OriginalNestedSeparators;

    public ArrayNestedSeparatorsTests()
    {
        // ArrayDataProcessor 是 DataTableProcessor 的嵌套类，NestedSeparators 为 internal 静态字段
        var dataTableProcessorType = typeof(DataTableProcessor);
        var arrayProcessorType = dataTableProcessorType.GetNestedType("ArrayDataProcessor", BindingFlags.Public)!;
        m_NestedSeparatorsField = arrayProcessorType.GetField("NestedSeparators", BindingFlags.Static | BindingFlags.NonPublic)!;
        m_NestedSeparatorsField.Should().NotBeNull("ArrayDataProcessor 应暴露 NestedSeparators 静态字段供配置");
        m_OriginalNestedSeparators = m_NestedSeparatorsField.GetValue(null);
    }

    public void Dispose()
    {
        // 恢复，避免污染其他测试
        m_NestedSeparatorsField.SetValue(null, m_OriginalNestedSeparators);
    }

    private void SetSeparators(string? separators)
    {
        m_NestedSeparatorsField.SetValue(null, separators);
    }

    private static DataTableProcessor.DataProcessor GetProcessor(string typeString)
    {
        var dataTableProcessorType = typeof(DataTableProcessor);
        var utilityType = dataTableProcessorType.GetNestedType("DataProcessorUtility", BindingFlags.NonPublic)!;
        var method = utilityType.GetMethod("GetDataProcessor", BindingFlags.Public | BindingFlags.Static)!;
        return (DataTableProcessor.DataProcessor)method.Invoke(null, new object[] { typeString })!;
    }

    private static byte[] WriteValue(string typeString, string value)
    {
        var processor = GetProcessor(typeString);
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        processor.WriteToStream(bw, value);
        bw.Flush();
        return ms.ToArray();
    }

    private static int[][] ReadNestedIntArray(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var br = new BinaryReader(ms);
        var outerCount = br.Read7BitEncodedInt32();
        var arr = new int[outerCount][];
        for (int i = 0; i < outerCount; i++)
        {
            var innerCount = br.Read7BitEncodedInt32();
            arr[i] = new int[innerCount];
            for (int j = 0; j < innerCount; j++)
            {
                arr[i][j] = br.Read7BitEncodedInt32();
            }
        }
        return arr;
    }

    private static int[][][] ReadTripleNestedIntArray(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var br = new BinaryReader(ms);
        var d1 = br.Read7BitEncodedInt32();
        var arr = new int[d1][][];
        for (int i = 0; i < d1; i++)
        {
            var d2 = br.Read7BitEncodedInt32();
            arr[i] = new int[d2][];
            for (int j = 0; j < d2; j++)
            {
                var d3 = br.Read7BitEncodedInt32();
                arr[i][j] = new int[d3];
                for (int k = 0; k < d3; k++)
                {
                    arr[i][j][k] = br.Read7BitEncodedInt32();
                }
            }
        }
        return arr;
    }

    [Fact]
    public void NestedArray_PipeOuter_HashInner_SingleEntry()
    {
        // 场景：id#count|id#count；只有一条目时格式为 "801000#2"，
        // 期望被建模为 [[801000, 2]]，而不是 [[801000],[2]]
        SetSeparators("|#");

        var bytes = WriteValue("array<array<int>>", "801000#2");
        ReadNestedIntArray(bytes).Should().BeEquivalentTo(new[] { new[] { 801000, 2 } });
    }

    [Fact]
    public void NestedArray_PipeOuter_HashInner_MultipleEntries()
    {
        SetSeparators("|#");

        var bytes = WriteValue("array<array<int>>", "1#2|3#4|5#6");
        ReadNestedIntArray(bytes).Should().BeEquivalentTo(new[]
        {
            new[] { 1, 2 },
            new[] { 3, 4 },
            new[] { 5, 6 },
        });
    }

    [Fact]
    public void NestedArray_HashOuter_PipeInner()
    {
        // 用户示例：第一层 '#'，第二层 '|'
        SetSeparators("#|");

        var bytes = WriteValue("array<array<int>>", "1|2#3|4");
        ReadNestedIntArray(bytes).Should().BeEquivalentTo(new[]
        {
            new[] { 1, 2 },
            new[] { 3, 4 },
        });
    }

    [Fact]
    public void TripleNestedArray_UsesPerDepthSeparators()
    {
        // 第 1 层 '#'，第 2 层 '|'，第 3 层 '-'
        SetSeparators("#|-");

        // 期望 [ [[1,2],[3,4]], [[5,6]] ]
        // 最外层用 '#' 分两组：
        //   组1: "1-2|3-4"  -> 内层用 '|' 分两组: "1-2", "3-4" -> 最内层用 '-' 分: [1,2], [3,4]
        //   组2: "5-6"      -> 内层用 '|' 分一组: "5-6" (无 '|', 单元素) -> 最内层用 '-' 分: [5,6]
        var bytes = WriteValue("array<array<array<int>>>", "1-2|3-4#5-6");
        ReadTripleNestedIntArray(bytes).Should().BeEquivalentTo(new[]
        {
            new[] { new[] { 1, 2 }, new[] { 3, 4 } },
            new[] { new[] { 5, 6 } },
        });
    }

    [Fact]
    public void NestedArray_SingleSeparatorReused_WhenDepthExceedsConfig()
    {
        // 仅配置 1 个分隔符；超出深度时复用最后一个字符（向后兼容退路）
        SetSeparators("#");

        var bytes = WriteValue("array<array<int>>", "1#2");
        // 最外层用 '#' 分： "1", "2"；内层也复用 '#'，单元素 -> [[1],[2]]
        ReadNestedIntArray(bytes).Should().BeEquivalentTo(new[]
        {
            new[] { 1 },
            new[] { 2 },
        });
    }

    [Fact]
    public void NestedArray_EmptyConfig_FallsBackToLegacyPipePriority()
    {
        // 未配置时维持旧逻辑：优先 '|'
        SetSeparators(string.Empty);

        var bytes = WriteValue("array<array<int>>", "1#2|3#4");
        // 旧逻辑：外层有 '|' 优先按 '|' 切 -> "1#2", "3#4"；内层无 '|' 用 '#' -> [[1,2],[3,4]]
        ReadNestedIntArray(bytes).Should().BeEquivalentTo(new[]
        {
            new[] { 1, 2 },
            new[] { 3, 4 },
        });
    }

    [Fact]
    public void ApplyArraySeparatorOptions_NullWhenOptionIsEmpty()
    {
        // 构造 DataTableProcessor 时空字符串应被规范化为 null（即兼容模式）
        var options = new ParseOptions { ArrayNestedSeparators = string.Empty };
        var ctx = new GenerationContext();
        _ = new DataTableProcessor(ctx, null!, options, new DiagnosticsCollector());

        m_NestedSeparatorsField.GetValue(null).Should().BeNull();
    }

    [Fact]
    public void ApplyArraySeparatorOptions_SetsFromOptions()
    {
        var options = new ParseOptions { ArrayNestedSeparators = "#|-" };
        var ctx = new GenerationContext();
        _ = new DataTableProcessor(ctx, null!, options, new DiagnosticsCollector());

        m_NestedSeparatorsField.GetValue(null).Should().Be("#|-");
    }
}
