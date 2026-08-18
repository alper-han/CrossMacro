
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public sealed class PortalPipeWireFrame : IDisposable
{
    private readonly IDisposable? _owner;
    private bool _disposed;

    public PortalPipeWireFrame(
        ScreenRect logicalBounds,
        int stride,
        ScreenPixelFormat pixelFormat,
        ReadOnlyMemory<byte> pixels,
        IDisposable? owner = null,
        ReadOnlyMemory<byte> validPixelMask = default)
    {
        var bytesPerPixel = ScreenFrame.GetBytesPerPixel(pixelFormat);
        var minimumStride = checked(logicalBounds.Width * bytesPerPixel);
        if (stride < minimumStride)
        {
            throw new ArgumentOutOfRangeException(nameof(stride), stride, "Portal PipeWire frame stride is smaller than its logical row width.");
        }

        var minimumLength = checked(stride * logicalBounds.Height);
        if (pixels.Length < minimumLength)
        {
            throw new ArgumentException("Portal PipeWire frame pixel memory is smaller than the declared frame dimensions.", nameof(pixels));
        }

        var pixelCount = checked(logicalBounds.Width * logicalBounds.Height);
        if (!validPixelMask.IsEmpty && validPixelMask.Length < pixelCount)
        {
            throw new ArgumentException("Portal PipeWire valid-pixel mask is smaller than the declared frame dimensions.", nameof(validPixelMask));
        }

        LogicalBounds = logicalBounds;
        Stride = stride;
        PixelFormat = pixelFormat;
        Pixels = pixels;
        ValidPixelMask = validPixelMask.IsEmpty ? ReadOnlyMemory<byte>.Empty : validPixelMask.Slice(0, pixelCount);
        _owner = owner;
    }

    public ScreenRect LogicalBounds { get; }

    public int Stride { get; }

    public ScreenPixelFormat PixelFormat { get; }

    public ReadOnlyMemory<byte> Pixels { get; }

    public ReadOnlyMemory<byte> ValidPixelMask { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _owner?.Dispose();
    }
}
