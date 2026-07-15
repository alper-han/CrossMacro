
namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public sealed class WaylandScreenFrameComposerTests
{
    [Theory]
    [MemberData(nameof(PixelFormatCases))]
    public void CopySource_ConvertsSupportedFormatsToBgraExplicitly(
        ScreenPixelFormat sourceFormat,
        byte[] sourceBytes,
        byte[] expectedBgra)
    {
        using var composedFrame = ComposeSinglePixel(sourceFormat, sourceBytes);

        Assert.Equal(expectedBgra, composedFrame.Pixels.Span.ToArray());
        Assert.Empty(composedFrame.ValidPixelMask.ToArray());
        Assert.True(composedFrame.IsFullyValid);
    }

    [Theory]
    [MemberData(nameof(PixelFormatCases))]
    public void CopySource_UsesDeclaredPhysicalWidthAndExcludesPaddedRows(
        ScreenPixelFormat sourceFormat,
        byte[] sourceBytes,
        byte[] expectedBgra)
    {
        var bytesPerPixel = ScreenFrame.GetBytesPerPixel(sourceFormat);
        var stride = checked(bytesPerPixel * 3);
        var source = new byte[stride];
        sourceBytes.CopyTo(source, bytesPerPixel);
        Array.Fill(source, (byte)0xEE, bytesPerPixel * 2, bytesPerPixel);

        using var composer = WaylandScreenFrameComposer.Create(new ScreenRect(0, 0, 1, 1));
        composer.CopySource(source, stride, sourceFormat, 2, 1, new ScreenRect(0, 0, 1, 1), new ScreenRect(0, 0, 1, 1));

        using var composedFrame = composer.Complete();

        Assert.Equal(expectedBgra, composedFrame.Pixels.Span.ToArray());
    }

    [Theory]
    [MemberData(nameof(PixelFormatCases))]
    public void CopySource_RejectsShortPhysicalBufferOrStride(
        ScreenPixelFormat sourceFormat,
        byte[] sourceBytes,
        byte[] _)
    {
        var bytesPerPixel = ScreenFrame.GetBytesPerPixel(sourceFormat);
        var bounds = new ScreenRect(0, 0, 1, 1);
        using var composer = WaylandScreenFrameComposer.Create(bounds);

        Assert.Throws<ArgumentOutOfRangeException>(() => composer.CopySource(
            sourceBytes,
            (bytesPerPixel * 2) - 1,
            sourceFormat,
            2,
            1,
            bounds,
            bounds));

        Assert.Throws<ArgumentException>(() => composer.CopySource(
            new byte[(bytesPerPixel * 2) - 1],
            bytesPerPixel * 2,
            sourceFormat,
            2,
            1,
            bounds,
            bounds));
    }

    [Fact]
    public void CopySource_ComposesMultiOutputGeometryWithGapsAndNegativeCoordinates()
    {
        using var composer = WaylandScreenFrameComposer.Create(new ScreenRect(-1, 1, 5, 2));
        composer.CopySource(
            CreateRgbPixels([
                [Red, Red],
                [Red, Red],
            ]),
            sourceStride: 6,
            ScreenPixelFormat.Rgb24,
            sourcePhysicalWidth: 2,
            sourcePhysicalHeight: 2,
            new ScreenRect(-1, 1, 2, 2),
            new ScreenRect(-1, 1, 2, 2));
        composer.CopySource(
            CreateRgbPixels([[Blue, Blue]]),
            sourceStride: 6,
            ScreenPixelFormat.Rgb24,
            sourcePhysicalWidth: 2,
            sourcePhysicalHeight: 1,
            new ScreenRect(2, 1, 2, 1),
            new ScreenRect(2, 1, 2, 1));

        using var composedFrame = composer.Complete();
        using var frame = CreateScreenFrame(composedFrame);

        Assert.Equal(new ScreenRect(-1, 1, 5, 2), composedFrame.LogicalBounds);
        Assert.Equal(
            new byte[] { 1, 1, 0, 1, 1, 1, 1, 0, 0, 0 },
            composedFrame.ValidPixelMask.Span.ToArray());
        Assert.False(composedFrame.IsFullyValid);
        Assert.True(frame.TryGetPixel(new ScreenPoint(-1, 1), out var red));
        Assert.Equal(Red, red);
        Assert.True(frame.TryGetPixel(new ScreenPoint(2, 1), out var blue));
        Assert.Equal(Blue, blue);
        Assert.False(frame.TryGetPixel(new ScreenPoint(1, 1), out _));
        Assert.False(frame.TryGetPixel(new ScreenPoint(2, 2), out _));
        Assert.Null(frame.SearchPixel(composedFrame.LogicalBounds, Black));
        Assert.False(frame.IsRectangleFullyValid(new ScreenRect(1, 1, 2, 1)));
    }

    [Fact]
    public void CopySource_MapsPhysicalScalingToLogicalPixels()
    {
        using var composer = WaylandScreenFrameComposer.Create(new ScreenRect(10, 20, 2, 1));
        composer.CopySource(
            CreateRgbPixels([[Red, Red, Blue, Blue]]),
            sourceStride: 12,
            ScreenPixelFormat.Rgb24,
            sourcePhysicalWidth: 4,
            sourcePhysicalHeight: 1,
            new ScreenRect(10, 20, 2, 1),
            new ScreenRect(10, 20, 2, 1));

        using var composedFrame = composer.Complete();
        using var frame = CreateScreenFrame(composedFrame);

        Assert.True(frame.TryGetPixel(new ScreenPoint(10, 20), out var left));
        Assert.Equal(Red, left);
        Assert.True(frame.TryGetPixel(new ScreenPoint(11, 20), out var right));
        Assert.Equal(Blue, right);
    }

    [Fact]
    public void CopySource_ComposesDifferentSizedOutputsWithNonZeroRequestOriginAndPartialOutOfBounds()
    {
        using var composer = WaylandScreenFrameComposer.Create(new ScreenRect(-2, -1, 8, 4));
        composer.CopySource(
            CreateRgbPixels([
                [Red, Red, Red],
                [Red, Red, Red],
            ]),
            sourceStride: 9,
            ScreenPixelFormat.Rgb24,
            sourcePhysicalWidth: 3,
            sourcePhysicalHeight: 2,
            new ScreenRect(0, 0, 3, 2),
            new ScreenRect(0, 0, 3, 2));
        composer.CopySource(
            CreateRgbPixels([
                [Blue, Blue],
                [Blue, Blue],
                [Blue, Blue],
                [Blue, Blue],
            ]),
            sourceStride: 6,
            ScreenPixelFormat.Rgb24,
            sourcePhysicalWidth: 2,
            sourcePhysicalHeight: 4,
            new ScreenRect(4, -1, 2, 4),
            new ScreenRect(4, -1, 2, 4));

        using var composedFrame = composer.Complete();
        using var frame = CreateScreenFrame(composedFrame);

        Assert.Equal(new byte[]
        {
            0, 0, 0, 0, 0, 0, 1, 1,
            0, 0, 1, 1, 1, 0, 1, 1,
            0, 0, 1, 1, 1, 0, 1, 1,
            0, 0, 0, 0, 0, 0, 1, 1,
        }, composedFrame.ValidPixelMask.Span.ToArray());
        Assert.True(frame.TryGetPixel(new ScreenPoint(0, 0), out var red));
        Assert.Equal(Red, red);
        Assert.True(frame.TryGetPixel(new ScreenPoint(4, -1), out var blue));
        Assert.Equal(Blue, blue);
        Assert.False(frame.TryGetPixel(new ScreenPoint(-2, -1), out _));
        Assert.False(frame.TryGetPixel(new ScreenPoint(3, 0), out _));
    }

    [Fact]
    public void CopySource_LeavesUnselectedOutputAndGapInvalid()
    {
        using var composer = WaylandScreenFrameComposer.Create(new ScreenRect(0, 0, 7, 1));
        composer.CopySource(
            CreateRgbPixels([[Red, Red]]),
            sourceStride: 6,
            ScreenPixelFormat.Rgb24,
            sourcePhysicalWidth: 2,
            sourcePhysicalHeight: 1,
            new ScreenRect(0, 0, 2, 1),
            new ScreenRect(0, 0, 2, 1));
        composer.CopySource(
            CreateRgbPixels([[Blue, Blue]]),
            sourceStride: 6,
            ScreenPixelFormat.Rgb24,
            sourcePhysicalWidth: 2,
            sourcePhysicalHeight: 1,
            new ScreenRect(5, 0, 2, 1),
            new ScreenRect(5, 0, 2, 1));

        using var composedFrame = composer.Complete();
        using var frame = CreateScreenFrame(composedFrame);

        Assert.Equal(new byte[] { 1, 1, 0, 0, 0, 1, 1 }, composedFrame.ValidPixelMask.Span.ToArray());
        Assert.False(frame.ContainsAnyValidPixel(new ScreenRect(2, 0, 3, 1)));
        Assert.Null(frame.SearchPixel(new ScreenRect(0, 0, 7, 1), Black));
    }

    [Fact]
    public void Union_ComposesFourMixedOrientationOutputsWithSignedCoordinates()
    {
        var bounds = WaylandScreenFrameComposer.Union(
            new ScreenRect(-7, 4, 3, 8),
            new ScreenRect(0, -5, 12, 4));
        bounds = WaylandScreenFrameComposer.Union(bounds, new ScreenRect(15, 2, 5, 11));
        bounds = WaylandScreenFrameComposer.Union(bounds, new ScreenRect(-4, 15, 8, 3));

        Assert.Equal(new ScreenRect(-7, -5, 27, 23), bounds);
    }

    [Fact]
    public void Intersect_ReturnsNullForInternalGapAndOutsideRegion()
    {
        var left = new ScreenRect(-4, 0, 2, 3);
        var right = new ScreenRect(2, 0, 3, 3);

        Assert.Null(WaylandScreenFrameComposer.Intersect(left, new ScreenRect(-1, 0, 2, 3)));
        Assert.Null(WaylandScreenFrameComposer.Intersect(left, new ScreenRect(10, 0, 2, 3)));
        Assert.NotNull(WaylandScreenFrameComposer.Intersect(right, new ScreenRect(2, 0, 3, 3)));
    }

    [Fact]
    public void Create_RejectsCanvasBudgetBeforeRentingPoolArrays()
    {
        const int width = WaylandScreenFrameComposer.MaxCanvasPixels;
        var bounds = new ScreenRect(0, 0, width, 2);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => WaylandScreenFrameComposer.Create(bounds));

        Assert.Contains("stitched Wayland canvas", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CompleteTransfersPooledOwnershipAndDisposeIsIdempotent()
    {
        using var composer = WaylandScreenFrameComposer.Create(new ScreenRect(0, 0, 1, 1));
        composer.CopySource([0x12, 0x34, 0x56], 3, ScreenPixelFormat.Rgb24, 1, 1, new ScreenRect(0, 0, 1, 1), new ScreenRect(0, 0, 1, 1));
        using var composedFrame = composer.Complete();

        composer.Dispose();
        composer.Dispose();

        Assert.Equal(new byte[] { 0x56, 0x34, 0x12, 0xFF }, composedFrame.Pixels.ToArray());
        composedFrame.Dispose();
        composedFrame.Dispose();
    }

    public static TheoryData<ScreenPixelFormat, byte[], byte[]> PixelFormatCases => new()
    {
        { ScreenPixelFormat.Rgb24, [0x12, 0x34, 0x56], [0x56, 0x34, 0x12, 0xFF] },
        { ScreenPixelFormat.Bgr24, [0x56, 0x34, 0x12], [0x56, 0x34, 0x12, 0xFF] },
        { ScreenPixelFormat.Xrgb8888, [0x56, 0x34, 0x12, 0x00], [0x56, 0x34, 0x12, 0xFF] },
        { ScreenPixelFormat.Bgra8888, [0x56, 0x34, 0x12, 0x78], [0x56, 0x34, 0x12, 0x78] },
        { ScreenPixelFormat.Abgr8888, [0x12, 0x34, 0x56, 0x78], [0x56, 0x34, 0x12, 0x78] },
        { ScreenPixelFormat.Xbgr8888, [0x12, 0x34, 0x56, 0x00], [0x56, 0x34, 0x12, 0xFF] },
    };

    private static readonly ScreenPixelColor Black = new(0x00, 0x00, 0x00);
    private static readonly ScreenPixelColor Red = new(0xFF, 0x00, 0x00);
    private static readonly ScreenPixelColor Blue = new(0x00, 0x00, 0xFF);

    private static WaylandComposedFrame ComposeSinglePixel(ScreenPixelFormat sourceFormat, byte[] sourceBytes)
    {
        var bounds = new ScreenRect(0, 0, 1, 1);
        using var composer = WaylandScreenFrameComposer.Create(bounds);
        composer.CopySource(sourceBytes, sourceBytes.Length, sourceFormat, 1, 1, bounds, bounds);
        return composer.Complete();
    }

    private static ScreenFrame CreateScreenFrame(WaylandComposedFrame composedFrame)
    {
        return new ScreenFrame(
            composedFrame.LogicalBounds,
            composedFrame.Stride,
            composedFrame.PixelFormat,
            composedFrame.Pixels,
            validPixelMask: composedFrame.ValidPixelMask,
            validityIndex: composedFrame.ValidityIndex);
    }

    private static byte[] CreateRgbPixels(ScreenPixelColor[][] rows)
    {
        var width = rows[0].Length;
        var bytes = new byte[checked(width * rows.Length * 3)];
        for (var y = 0; y < rows.Length; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = checked(((y * width) + x) * 3);
                bytes[offset] = rows[y][x].R;
                bytes[offset + 1] = rows[y][x].G;
                bytes[offset + 2] = rows[y][x].B;
            }
        }

        return bytes;
    }
}
