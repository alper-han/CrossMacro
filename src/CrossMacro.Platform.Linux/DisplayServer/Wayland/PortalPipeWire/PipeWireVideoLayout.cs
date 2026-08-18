namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.PortalPipeWire;

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct PipeWireVideoLayout(int Width, int Height, PipeWireVideoFormat Format)
{
    public int BytesPerPixel => Format is PipeWireVideoFormat.Rgb or PipeWireVideoFormat.Bgr ? 3 : 4;

    public int MinimumStride => checked(Width * BytesPerPixel);

    public int MinimumBufferSize => checked(MinimumStride * Height);

    public void WriteXrgb(ReadOnlySpan<byte> source, int sourceOffset, Span<byte> target, int targetOffset)
    {
        var (red, green, blue) = Format switch
        {
            PipeWireVideoFormat.Rgbx or PipeWireVideoFormat.Rgba or PipeWireVideoFormat.Rgb => (0, 1, 2),
            PipeWireVideoFormat.Bgrx or PipeWireVideoFormat.Bgra or PipeWireVideoFormat.Bgr => (2, 1, 0),
            PipeWireVideoFormat.Xrgb or PipeWireVideoFormat.Argb => (1, 2, 3),
            PipeWireVideoFormat.Xbgr or PipeWireVideoFormat.Abgr => (3, 2, 1),
            _ => throw new InvalidOperationException($"Unsupported PipeWire video format '{Format}'."),
        };

        target[targetOffset] = source[sourceOffset + blue];
        target[targetOffset + 1] = source[sourceOffset + green];
        target[targetOffset + 2] = source[sourceOffset + red];
        target[targetOffset + 3] = byte.MaxValue;
    }
}
