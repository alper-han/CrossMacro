namespace CrossMacro.Platform.Linux.Tests.PlatformAbstractions;

public class ScreenFrameTests
{
    [Fact]
    public void GetPixel_UsesStrideAndGlobalLogicalCoordinates()
    {
        var frame = new ScreenFrame(
            new ScreenRect(10, 20, 2, 2),
            stride: 8,
            ScreenPixelFormat.Rgb24,
            new byte[]
            {
                0xFF, 0x00, 0x00, 0xAA, 0xBB, 0xCC, 0x99, 0x99,
                0x00, 0xFF, 0x00, 0x00, 0x00, 0xFF, 0x88, 0x88
            });

        var first = frame.GetPixel(new ScreenPoint(10, 20));
        var last = frame.GetPixel(new ScreenPoint(11, 21));

        Assert.Equal(new ScreenPixelColor(0xFF, 0x00, 0x00), first);
        Assert.Equal(new ScreenPixelColor(0x00, 0x00, 0xFF), last);
    }

    [Theory]
    [MemberData(nameof(ChannelOrderingCases))]
    public void GetPixel_NormalizesSupportedChannelOrdering(ScreenPixelFormat pixelFormat, byte[] pixels)
    {
        var frame = new ScreenFrame(new ScreenRect(0, 0, 1, 1), pixels.Length, pixelFormat, pixels);

        var color = frame.GetPixel(new ScreenPoint(0, 0));

        Assert.Equal(new ScreenPixelColor(0x12, 0x34, 0x56), color);
    }

    [Theory]
    [MemberData(nameof(FourthByteIgnoredCases))]
    public void GetPixel_IgnoresAlphaByte(ScreenPixelFormat pixelFormat, byte[] transparentPixels, byte[] opaquePixels)
    {
        var transparent = new ScreenFrame(new ScreenRect(0, 0, 1, 1), 4, pixelFormat, transparentPixels);
        var opaque = new ScreenFrame(new ScreenRect(0, 0, 1, 1), 4, pixelFormat, opaquePixels);

        var transparentColor = transparent.GetPixel(new ScreenPoint(0, 0));
        var opaqueColor = opaque.GetPixel(new ScreenPoint(0, 0));

        Assert.Equal(new ScreenPixelColor(0x12, 0x34, 0x56), transparentColor);
        Assert.Equal(transparentColor, opaqueColor);
    }

    [Fact]
    public void SearchPixel_ReturnsFirstRowMajorMatchInGlobalCoordinates()
    {
        var expected = new ScreenPixelColor(0x10, 0x20, 0x30);
        var frame = new ScreenFrame(
            new ScreenRect(100, 50, 3, 2),
            stride: 12,
            ScreenPixelFormat.Bgra8888,
            new byte[]
            {
                0x00, 0x00, 0x00, 0xFF,
                0x30, 0x20, 0x10, 0x00,
                0x30, 0x20, 0x10, 0xFF,
                0x77, 0x77, 0x77, 0x77,
                0x30, 0x20, 0x10, 0xAA,
                0x00, 0x00, 0x00, 0xFF,
                0x30, 0x20, 0x10, 0x55,
                0x66, 0x66, 0x66, 0x66
            });

        var found = frame.SearchPixel(new ScreenRect(100, 50, 3, 2), expected);

        Assert.Equal(new ScreenPixelSearchMatch(new ScreenPoint(101, 50), expected), found);
    }

    [Fact]
    public void SearchPixel_ReturnsNullWhenColorIsAbsent()
    {
        var frame = new ScreenFrame(
            new ScreenRect(0, 0, 2, 1),
            stride: 6,
            ScreenPixelFormat.Rgb24,
            new byte[] { 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF });

        var found = frame.SearchPixel(new ScreenRect(0, 0, 2, 1), new ScreenPixelColor(0x10, 0x20, 0x30));

        Assert.Null(found);
    }

    [Fact]
    public void SearchPixel_WhenColorIsWithinTolerance_ReturnsActualMatchedColor()
    {
        var frame = new ScreenFrame(
            new ScreenRect(0, 0, 2, 1),
            stride: 6,
            ScreenPixelFormat.Rgb24,
            new byte[] { 0x00, 0x00, 0x00, 0xF7, 0x06, 0xA1 });

        var found = frame.SearchPixel(
            new ScreenRect(0, 0, 2, 1),
            new ScreenPixelColor(0xFF, 0x00, 0xAA),
            tolerance: 10);

        Assert.Equal(
            new ScreenPixelSearchMatch(new ScreenPoint(1, 0), new ScreenPixelColor(0xF7, 0x06, 0xA1)),
            found);
    }

    [Fact]
    public void SearchPixel_WhenOneChannelExceedsTolerance_ReturnsNull()
    {
        var frame = new ScreenFrame(
            new ScreenRect(0, 0, 1, 1),
            stride: 3,
            ScreenPixelFormat.Rgb24,
            new byte[] { 0xE0, 0x00, 0xAA });

        var found = frame.SearchPixel(
            new ScreenRect(0, 0, 1, 1),
            new ScreenPixelColor(0xFF, 0x00, 0xAA),
            tolerance: 10);

        Assert.Null(found);
    }

    [Fact]
    public void TryGetPixel_ReturnsFalseForOutOfBoundsGlobalCoordinate()
    {
        var frame = new ScreenFrame(
            new ScreenRect(5, 6, 1, 1),
            stride: 3,
            ScreenPixelFormat.Rgb24,
            new byte[] { 0x01, 0x02, 0x03 });

        var found = frame.TryGetPixel(new ScreenPoint(4, 6), out var color);

        Assert.False(found);
        Assert.Equal(default, color);
    }

    [Fact]
    public void GetPixel_ThrowsForOutOfBoundsGlobalCoordinate()
    {
        var frame = new ScreenFrame(
            new ScreenRect(5, 6, 1, 1),
            stride: 3,
            ScreenPixelFormat.Rgb24,
            new byte[] { 0x01, 0x02, 0x03 });

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => frame.GetPixel(new ScreenPoint(6, 6)));

        Assert.Equal("point", exception.ParamName);
    }

    [Fact]
    public void SearchPixel_ThrowsWhenRegionIsOutsideFrameBounds()
    {
        var frame = new ScreenFrame(
            new ScreenRect(5, 6, 2, 2),
            stride: 6,
            ScreenPixelFormat.Rgb24,
            new byte[12]);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => frame.SearchPixel(new ScreenRect(6, 6, 2, 1), new ScreenPixelColor(0x00, 0x00, 0x00)));

        Assert.Equal("region", exception.ParamName);
    }

    public static TheoryData<ScreenPixelFormat, byte[]> ChannelOrderingCases => new()
    {
        { ScreenPixelFormat.Rgb24, new byte[] { 0x12, 0x34, 0x56 } },
        { ScreenPixelFormat.Bgr24, new byte[] { 0x56, 0x34, 0x12 } },
        { ScreenPixelFormat.Xrgb8888, new byte[] { 0x56, 0x34, 0x12, 0x00 } },
        { ScreenPixelFormat.Bgra8888, new byte[] { 0x56, 0x34, 0x12, 0xFF } },
        { ScreenPixelFormat.Abgr8888, new byte[] { 0x12, 0x34, 0x56, 0x00 } },
        { ScreenPixelFormat.Xbgr8888, new byte[] { 0x12, 0x34, 0x56, 0xFF } }
    };

    public static TheoryData<ScreenPixelFormat, byte[], byte[]> FourthByteIgnoredCases => new()
    {
        { ScreenPixelFormat.Xrgb8888, new byte[] { 0x56, 0x34, 0x12, 0x00 }, new byte[] { 0x56, 0x34, 0x12, 0xFF } },
        { ScreenPixelFormat.Bgra8888, new byte[] { 0x56, 0x34, 0x12, 0x00 }, new byte[] { 0x56, 0x34, 0x12, 0xFF } },
        { ScreenPixelFormat.Abgr8888, new byte[] { 0x12, 0x34, 0x56, 0x00 }, new byte[] { 0x12, 0x34, 0x56, 0xFF } },
        { ScreenPixelFormat.Xbgr8888, new byte[] { 0x12, 0x34, 0x56, 0x00 }, new byte[] { 0x12, 0x34, 0x56, 0xFF } }
    };
}
