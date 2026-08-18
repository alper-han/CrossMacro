namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.PortalPipeWire;

internal static class PipeWireFrameRowConverter
{
    public static void Convert(
        ReadOnlySpan<byte> sourceRow,
        PipeWireVideoLayout layout,
        int sourceLogicalWidth,
        int sourceStartX,
        Span<byte> targetRow)
    {
        if (sourceLogicalWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceLogicalWidth), sourceLogicalWidth, "Source logical width must be positive.");
        }

        if (sourceRow.Length < layout.MinimumStride)
        {
            throw new ArgumentException("Source row is smaller than the negotiated pixel width.", nameof(sourceRow));
        }

        if (targetRow.Length % PipeWireConstants.Xrgb8888BytesPerPixel is not 0)
        {
            throw new ArgumentException("Target row is not aligned to canonical XRGB pixels.", nameof(targetRow));
        }

        var targetPixelCount = targetRow.Length / PipeWireConstants.Xrgb8888BytesPerPixel;
        if (sourceStartX < 0 || sourceStartX > sourceLogicalWidth - targetPixelCount)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceStartX), sourceStartX, "Requested logical row is outside the source extent.");
        }

        for (var targetX = 0; targetX < targetPixelCount; targetX++)
        {
            var sourceX = WaylandLogicalPhysicalMapper.MapPixel(sourceStartX + targetX, sourceLogicalWidth, layout.Width);
            layout.WriteXrgb(
                sourceRow,
                sourceX * layout.BytesPerPixel,
                targetRow,
                targetX * PipeWireConstants.Xrgb8888BytesPerPixel);
        }
    }
}
