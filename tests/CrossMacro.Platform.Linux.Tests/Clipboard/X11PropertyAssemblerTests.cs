using CrossMacro.Platform.Linux.Clipboard;

namespace CrossMacro.Platform.Linux.Tests.Clipboard;

public sealed class X11PropertyAssemblerTests
{
    private const int AtomType = 31;

    [Fact]
    public void ReadAll_WhenPropertyFitsInOneReply_ReturnsItWithoutAskingForMore()
    {
        var offsets = new List<long>();

        var result = X11PropertyAssembler.ReadAll(offset =>
        {
            offsets.Add(offset);
            return new X11PropertyChunk(AtomType, [1, 2, 3], WireBytesRead: 3, BytesAfter: 0);
        });

        Assert.Equal([0L], offsets);
        Assert.Equal((nuint)AtomType, result.Type);
        Assert.Equal([1, 2, 3], result.Data);
    }

    [Fact]
    public void ReadAll_WhenServerReportsBytesAfter_ContinuesAtTheNextWireOffsetUntilDrained()
    {
        var property = Enumerable.Range(0, 22).Select(static i => (byte)i).ToArray();
        var offsets = new List<long>();

        var result = X11PropertyAssembler.ReadAll(offset =>
        {
            offsets.Add(offset);
            return ServeRange(property, offset, maxUnits: 2);
        });

        Assert.Equal([0L, 2L, 4L], offsets);
        Assert.Equal(property, result.Data);
        Assert.Equal((nuint)AtomType, result.Type);
    }

    [Fact]
    public void ReadAll_WhenItemsAreLongerThanTheirWireSize_AdvancesByTheWireBytesRead()
    {
        // Format-32 items are 8-byte longs in Xlib memory but 4 bytes on the wire.
        var offsets = new List<long>();
        var replies = new Queue<X11PropertyChunk>(
        [
            new X11PropertyChunk(AtomType, new byte[16], WireBytesRead: 8, BytesAfter: 4),
            new X11PropertyChunk(AtomType, new byte[8], WireBytesRead: 4, BytesAfter: 0),
        ]);

        var result = X11PropertyAssembler.ReadAll(offset =>
        {
            offsets.Add(offset);
            return replies.Dequeue();
        });

        Assert.Equal([0L, 2L], offsets);
        Assert.Equal(24, result.Data.Length);
    }

    [Fact]
    public void ReadAll_WhenPropertyExceedsTheLimit_Throws()
    {
        var property = new byte[64];

        var exception = Assert.Throws<InvalidOperationException>(() =>
            X11PropertyAssembler.ReadAll(offset => ServeRange(property, offset, maxUnits: 4), maxBytes: 32));

        Assert.Contains("limit", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadAll_WhenServerReportsMoreDataButReturnsNone_Throws()
    {
        _ = Assert.Throws<InvalidOperationException>(() =>
            X11PropertyAssembler.ReadAll(static _ => new X11PropertyChunk(AtomType, [], WireBytesRead: 0, BytesAfter: 8)));
    }

    [Theory]
    [InlineData(8, 1)]
    [InlineData(16, 2)]
    public void ItemBytes_ForNarrowFormats_MatchesTheFormatWidth(int format, int expected)
    {
        Assert.Equal(expected, X11PropertyAssembler.ItemBytes(format));
    }

    [Fact]
    public void ItemBytes_ForFormat32_IsTheNativeLongSize()
    {
        Assert.Equal(nint.Size, X11PropertyAssembler.ItemBytes(32));
    }

    [Fact]
    public void ItemBytes_ForUnknownFormat_Throws()
    {
        _ = Assert.Throws<InvalidOperationException>(() => X11PropertyAssembler.ItemBytes(24));
    }

    private static X11PropertyChunk ServeRange(byte[] property, long offsetUnits, int maxUnits)
    {
        var start = checked((int)(offsetUnits * X11PropertyAssembler.WireUnitBytes));
        var length = Math.Min(property.Length - start, maxUnits * X11PropertyAssembler.WireUnitBytes);
        return new X11PropertyChunk(
            AtomType,
            property.AsSpan(start, length).ToArray(),
            WireBytesRead: length,
            BytesAfter: property.Length - start - length);
    }
}
