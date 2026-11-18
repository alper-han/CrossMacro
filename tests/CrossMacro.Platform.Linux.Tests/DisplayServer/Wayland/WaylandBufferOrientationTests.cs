namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public sealed class WaylandBufferOrientationTests
{
    // The upright image is
    //   A B C
    //   D E F
    // Each case lists the buffer the compositor produces for that wl_output.transform value.
    public static TheoryData<WaylandBufferTransform, string[]> TransformedBuffers => new()
    {
        { WaylandBufferTransform.Normal, ["ABC", "DEF"] },
        { WaylandBufferTransform.Rotate90, ["CF", "BE", "AD"] },
        { WaylandBufferTransform.Rotate180, ["FED", "CBA"] },
        { WaylandBufferTransform.Rotate270, ["DA", "EB", "FC"] },
        { WaylandBufferTransform.Flipped, ["CBA", "FED"] },
        { WaylandBufferTransform.FlippedRotate90, ["AD", "BE", "CF"] },
        { WaylandBufferTransform.FlippedRotate180, ["DEF", "ABC"] },
        { WaylandBufferTransform.FlippedRotate270, ["FC", "EB", "DA"] },
    };

    [Theory]
    [MemberData(nameof(TransformedBuffers))]
    public void CopySource_RestoresTheUprightImageFromEveryTransform(WaylandBufferTransform transform, string[] bufferRows)
    {
        ArgumentNullException.ThrowIfNull(bufferRows);

        var bounds = new ScreenRect(0, 0, 3, 2);
        using var composer = WaylandScreenFrameComposer.Create(bounds);

        composer.CopySource(
            CreateBuffer(bufferRows),
            sourceStride: bufferRows[0].Length * 3,
            ScreenPixelFormat.Rgb24,
            sourcePhysicalWidth: bufferRows[0].Length,
            sourcePhysicalHeight: bufferRows.Length,
            bounds,
            bounds,
            transform);

        using var composed = composer.Complete();
        Assert.Equal("ABCDEF", ReadMarkers(composed));
    }

    [Fact]
    public void CopySource_ReadsTheYInvertedBufferTopToBottom()
    {
        var bounds = new ScreenRect(0, 0, 3, 2);
        using var composer = WaylandScreenFrameComposer.Create(bounds);

        composer.CopySource(
            CreateBuffer(["DEF", "ABC"]),
            sourceStride: 9,
            ScreenPixelFormat.Rgb24,
            sourcePhysicalWidth: 3,
            sourcePhysicalHeight: 2,
            bounds,
            bounds,
            WaylandBufferOrientation.FromYInvert(yInverted: true));

        using var composed = composer.Complete();
        Assert.Equal("ABCDEF", ReadMarkers(composed));
    }

    [Fact]
    public void CopySource_ScalesTheRotatedImageToTheLogicalBounds()
    {
        // The upright image is 4x2 (left half X, right half Y); the rotated buffer is 2x4.
        var logical = new ScreenRect(0, 0, 2, 1);
        using var composer = WaylandScreenFrameComposer.Create(logical);

        composer.CopySource(
            CreateBuffer(["YY", "YY", "XX", "XX"]),
            sourceStride: 6,
            ScreenPixelFormat.Rgb24,
            sourcePhysicalWidth: 2,
            sourcePhysicalHeight: 4,
            logical,
            logical,
            WaylandBufferTransform.Rotate90);

        using var composed = composer.Complete();
        Assert.Equal("XY", ReadMarkers(composed));
    }

    [Theory]
    [InlineData(0u, true, WaylandBufferTransform.Normal)]
    [InlineData(3u, true, WaylandBufferTransform.Rotate270)]
    [InlineData(7u, true, WaylandBufferTransform.FlippedRotate270)]
    [InlineData(8u, false, WaylandBufferTransform.Normal)]
    [InlineData(uint.MaxValue, false, WaylandBufferTransform.Normal)]
    public void TryParse_AcceptsOnlyDefinedTransforms(uint value, bool expectedValid, WaylandBufferTransform expected)
    {
        Assert.Equal(expectedValid, WaylandBufferOrientation.TryParse(value, out var transform));
        Assert.Equal(expected, transform);
    }

    [Theory]
    [InlineData(WaylandBufferTransform.Normal, 3, 2, 3, 2)]
    [InlineData(WaylandBufferTransform.Rotate90, 3, 2, 2, 3)]
    [InlineData(WaylandBufferTransform.Rotate180, 3, 2, 3, 2)]
    [InlineData(WaylandBufferTransform.Rotate270, 3, 2, 2, 3)]
    [InlineData(WaylandBufferTransform.Flipped, 3, 2, 3, 2)]
    [InlineData(WaylandBufferTransform.FlippedRotate90, 3, 2, 2, 3)]
    public void GetLogicalSize_SwapsAxesForQuarterTurns(WaylandBufferTransform transform, int bufferWidth, int bufferHeight, int width, int height)
    {
        Assert.Equal((width, height), WaylandBufferOrientation.GetLogicalSize(transform, bufferWidth, bufferHeight));
    }

    private static byte[] CreateBuffer(string[] rows)
    {
        var width = rows[0].Length;
        var bytes = new byte[width * rows.Length * 3];
        for (var y = 0; y < rows.Length; y++)
        {
            for (var x = 0; x < width; x++)
            {
                bytes[((y * width) + x) * 3] = (byte)rows[y][x];
            }
        }

        return bytes;
    }

    private static string ReadMarkers(WaylandComposedFrame frame)
    {
        var pixels = frame.Pixels.Span;
        var chars = new char[frame.LogicalBounds.Width * frame.LogicalBounds.Height];
        for (var y = 0; y < frame.LogicalBounds.Height; y++)
        {
            for (var x = 0; x < frame.LogicalBounds.Width; x++)
            {
                // The composed frame is BGRA, so the red marker byte is at offset 2.
                chars[(y * frame.LogicalBounds.Width) + x] = (char)pixels[(y * frame.Stride) + (x * 4) + 2];
            }
        }

        return new string(chars);
    }
}
