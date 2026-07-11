using System.IO;
using System.Text;
using FluentAssertions;
using Xunit;

namespace DataTables.Tests;

public sealed class DataTableBinaryFormatTests
{
    [Fact]
    public void HeaderCodec_ShouldRoundTripStructuredMetadata()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            var rowCountPosition = DataTableBinaryProtocol.WriteHeader(writer, 0x1234UL, "1.2.3", "Game.DTHero");
            DataTableBinaryProtocol.PatchRowCount(writer, rowCountPosition, 42);
        }

        stream.Position = 0;
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        var header = DataTableBinaryProtocol.ReadHeader(reader);

        header.Signature.Should().Be(DataTableBinaryProtocol.Signature);
        header.Version.Should().Be(DataTableBinaryProtocol.Version);
        header.SchemaHash.Should().Be(0x1234UL);
        header.GeneratorVersion.Should().Be("1.2.3");
        header.TableFullName.Should().Be("Game.DTHero");
        header.RowCount.Should().Be(42);
        header.Flags.Should().Be(DataTableBinaryProtocol.FlagsNone);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(65536)]
    public void HeaderCodec_ShouldRejectRowCountsOutsideProtocolRange(int rowCount)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        var rowCountPosition = DataTableBinaryProtocol.WriteHeader(writer, 1UL, "1.0", "Game.DTItem");

        var action = () => DataTableBinaryProtocol.PatchRowCount(writer, rowCountPosition, rowCount);

        action.Should().Throw<InvalidDataException>().WithMessage("*protocol limit*");
    }

    [Fact]
    public void HeaderCodec_ShouldRejectTruncatedHeadersWithProtocolException()
    {
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        var action = () => DataTableBinaryProtocol.ReadHeader(reader);

        action.Should().Throw<InvalidDataException>().WithMessage("*truncated or malformed*");
    }
}
