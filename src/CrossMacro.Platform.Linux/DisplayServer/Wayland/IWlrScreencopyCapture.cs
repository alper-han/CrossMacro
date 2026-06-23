using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public interface IWlrScreencopyCapture : IWlrScreencopySupportProbe, IDisposable
{
    Task<WlrScreencopyCaptureResult> CaptureRegionAsync(ScreenRect? region, ScreenReadOptions options);
}

public interface IWlrScreencopyNativeCaptureSessionFactory
{
    Task<WlrScreencopyCaptureResult> CaptureRegionAsync(ScreenRect? region, ScreenReadOptions options);
}

public sealed class WlrScreencopyFrame : IDisposable
{
    private readonly IDisposable? _owner;
    private bool _disposed;

    public WlrScreencopyFrame(
        ScreenRect logicalBounds,
        int stride,
        ScreenPixelFormat pixelFormat,
        ReadOnlyMemory<byte> pixels,
        IDisposable? owner = null)
    {
        var bytesPerPixel = ScreenFrame.GetBytesPerPixel(pixelFormat);
        var minimumStride = checked(logicalBounds.Width * bytesPerPixel);
        if (stride < minimumStride)
        {
            throw new ArgumentOutOfRangeException(nameof(stride), stride, "wlr-screencopy frame stride is smaller than its logical row width.");
        }

        var minimumLength = checked(stride * logicalBounds.Height);
        if (pixels.Length < minimumLength)
        {
            throw new ArgumentException("wlr-screencopy frame pixel memory is smaller than the declared frame dimensions.", nameof(pixels));
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

public readonly record struct WlrScreencopyCaptureResult
{
    private WlrScreencopyCaptureResult(WlrScreencopyFrame? frame, ScreenReadErrorKind? errorKind, string? errorMessage)
    {
        if (frame is null && string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new ArgumentException("Failed wlr-screencopy captures require a message.", nameof(errorMessage));
        }

        Frame = frame;
        ErrorKind = errorKind;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess => ErrorKind is null;

    public WlrScreencopyFrame? Frame { get; }

    public ScreenReadErrorKind? ErrorKind { get; }

    public string? ErrorMessage { get; }

    public static WlrScreencopyCaptureResult Success(WlrScreencopyFrame frame) =>
        new(frame ?? throw new ArgumentNullException(nameof(frame)), null, null);

    public static WlrScreencopyCaptureResult Failure(ScreenReadErrorKind errorKind, string errorMessage) =>
        new(null, errorKind, errorMessage);
}
