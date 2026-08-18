
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public sealed class KWinScreenShotFrame : IDisposable
{
    private readonly IDisposable? _owner;
    private bool _disposed;

    public KWinScreenShotFrame(ScreenRect logicalBounds, int stride, ScreenPixelFormat pixelFormat, ReadOnlyMemory<byte> pixels, IDisposable? owner = null)
    {
        var bytesPerPixel = ScreenFrame.GetBytesPerPixel(pixelFormat);
        var minimumStride = checked(logicalBounds.Width * bytesPerPixel);
        if (stride < minimumStride)
        {
            throw new ArgumentOutOfRangeException(nameof(stride), stride, "KWin screenshot frame stride is smaller than its logical row width.");
        }

        var minimumLength = checked(stride * logicalBounds.Height);
        if (pixels.Length < minimumLength)
        {
            throw new ArgumentException("KWin screenshot frame pixel memory is smaller than the declared frame dimensions.", nameof(pixels));
        }

        LogicalBounds = logicalBounds;
        Stride = stride;
        PixelFormat = pixelFormat;
        Pixels = pixels;
        _owner = owner;
    }

    public ScreenRect LogicalBounds { get; }
    public int Stride { get; }
    public ScreenPixelFormat PixelFormat { get; }
    public ReadOnlyMemory<byte> Pixels { get; }

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
