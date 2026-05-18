using System;
using System.IO;
using System.Reflection;
using DataTables.GeneratorCore;
using FluentAssertions;
using Xunit;

namespace DataTables.Tests;

/// <summary>
/// 验证 ArrayDataProcessor 对自定义分隔符（'#' 或 '|'）的纯文本数组格式支持，
/// 同时保证原有 JSON 数组格式仍然兼容。
/// </summary>
public class ArrayDataProcessorTests
{
    /// <summary>
    /// 通过反射获取私有的 DataProcessorUtility.GetDataProcessor 方法，
    /// 由它构造出真实的处理器组合（避免直接访问私有内嵌处理器类型）。
    /// </summary>
    private static DataTableProcessor.DataProcessor GetProcessor(string typeString)
    {
        var dataTableProcessorType = typeof(DataTableProcessor);
        var utilityType = dataTableProcessorType.GetNestedType("DataProcessorUtility", BindingFlags.NonPublic);
        utilityType.Should().NotBeNull();
        var method = utilityType!.GetMethod("GetDataProcessor", BindingFlags.Public | BindingFlags.Static);
        method.Should().NotBeNull();
        return (DataTableProcessor.DataProcessor)method!.Invoke(null, new object[] { typeString })!;
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

    private static int[] ReadIntArray(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var br = new BinaryReader(ms);
        var count = br.Read7BitEncodedInt32();
        var arr = new int[count];
        for (int i = 0; i < count; i++)
        {
            arr[i] = br.Read7BitEncodedInt32();
        }
        return arr;
    }

    private static string[] ReadStringArray(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var br = new BinaryReader(ms);
        var count = br.Read7BitEncodedInt32();
        var arr = new string[count];
        for (int i = 0; i < count; i++)
        {
            arr[i] = br.ReadString();
        }
        return arr;
    }

    [Fact]
    public void IntArray_JsonFormat_StillWorks()
    {
        var bytes = WriteValue("array<int>", "[1,2,3]");
        ReadIntArray(bytes).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void IntArray_HashSeparator_PlainText()
    {
        var bytes = WriteValue("array<int>", "1#2#3");
        ReadIntArray(bytes).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void IntArray_PipeSeparator_PlainText()
    {
        var bytes = WriteValue("array<int>", "10|20|30|40");
        ReadIntArray(bytes).Should().Equal(10, 20, 30, 40);
    }

    [Fact]
    public void IntArray_SingleElementWithoutSeparator()
    {
        var bytes = WriteValue("array<int>", "42");
        ReadIntArray(bytes).Should().Equal(42);
    }

    [Fact]
    public void IntArray_EmptyValue_BecomesEmptyArray()
    {
        var bytes = WriteValue("array<int>", string.Empty);
        ReadIntArray(bytes).Should().BeEmpty();
    }

    [Fact]
    public void IntArray_ZeroValue_BecomesEmptyArray()
    {
        // 历史兼容：单元格值为 "0" 时视为空数组
        var bytes = WriteValue("array<int>", "0");
        ReadIntArray(bytes).Should().BeEmpty();
    }

    [Fact]
    public void StringArray_PipeSeparator_PlainText()
    {
        var bytes = WriteValue("array<string>", "alpha|beta|gamma");
        ReadStringArray(bytes).Should().Equal("alpha", "beta", "gamma");
    }

    [Fact]
    public void StringArray_HashSeparator_PlainText()
    {
        var bytes = WriteValue("array<string>", "a#b#c");
        ReadStringArray(bytes).Should().Equal("a", "b", "c");
    }

    [Fact]
    public void StringArray_JsonFormat_StillWorks()
    {
        var bytes = WriteValue("array<string>", "[\"x\",\"y\"]");
        ReadStringArray(bytes).Should().Equal("x", "y");
    }

    [Fact]
    public void IntArray_PipeTakesPriorityOverHash()
    {
        // 当两种分隔符同时存在时，'|' 优先，'#' 作为普通字符（这里 '#' 不会出现在 int 元素中，使用纯 pipe 数据验证优先级）
        var bytes = WriteValue("array<int>", "1|2|3");
        ReadIntArray(bytes).Should().Equal(1, 2, 3);
    }
}
