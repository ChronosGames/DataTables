using System;
using System.IO;
using Xunit;

namespace DataTables.Tests;

public class BinaryExtensionTest
{
    [Theory]
    [InlineData(0, new byte[] { 0 })]
    [InlineData(127, new byte[] { 0x7F })] // 边界值：127
    [InlineData(128, new byte[] { 0x80, 0x01 })] // 边界值：128
    [InlineData(255, new byte[] { 0xFF, 0x01 })]
    [InlineData(16383, new byte[] { 0xFF, 0x7F })] // 边界值：16383
    [InlineData(16384, new byte[] { 0x80, 0x80, 0x01 })] // 边界值：16384
    [InlineData(int.MaxValue, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x07 })] // 最大值
    [InlineData(1102011, new byte[] { 187, 161, 67 })] // 1102011
    public void Write7BitEncodedInt32_WritesCorrectly(int value, byte[] expectedBytes)
    {
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);
        // 调用 Write7BitEncodedInt32 方法
        binaryWriter.Write7BitEncodedInt32(value);

        // 验证写入的数据是否正确
        Assert.Equal(expectedBytes, memoryStream.ToArray());
    }

    [Theory]
    [InlineData(new byte[] { 0 }, 0)]
    [InlineData(new byte[] { 0x7F }, 127)] // 边界值：127
    [InlineData(new byte[] { 0x80, 0x01 }, 128)] // 边界值：128
    [InlineData(new byte[] { 0xFF, 0x01 }, 255)]
    [InlineData(new byte[] { 0xFF, 0x7F }, 16383)] // 边界值：16383
    [InlineData(new byte[] { 0x80, 0x80, 0x01 }, 16384)] // 边界值：16384
    [InlineData(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x07 }, int.MaxValue)] // 最大值
    [InlineData(new byte[] { 187, 161, 67 }, 1102011)] // 最大值
    public void Read7BitEncodedInt32_ReadsCorrectly(byte[] inputBytes, int expectedValue)
    {
        using var memoryStream = new MemoryStream(inputBytes);
        using var binaryReader = new BinaryReader(memoryStream);

        // 调用 Read7BitEncodedInt32 方法
        int actualValue = binaryReader.Read7BitEncodedInt32();

        // 验证读取的值是否正确
        Assert.Equal(expectedValue, actualValue);
    }

    [Fact]
    public void Read7BitEncodedInt32_ThrowsException_WhenInvalidData()
    {
        // 模拟无效的数据 (超过最大有效字节数的情况)
        byte[] invalidData = new byte[] { 0x80, 0x80, 0x80, 0x80, 0x80, 0x80 };

        using var memoryStream = new MemoryStream(invalidData);
        using var binaryReader = new BinaryReader(memoryStream);

        // 验证是否抛出异常
        Assert.Throws<Exception>(() => binaryReader.Read7BitEncodedInt32());
    }
}
