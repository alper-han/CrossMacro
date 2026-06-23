using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.Linux.DisplayServer.X11;

public interface IX11ScreenCapture : IX11ScreenCaptureSupportProbe, IDisposable
{
    Task<X11ScreenCaptureResult> CaptureAsync(ScreenRect? region, ScreenReadOptions options);

    Task<X11ScreenCaptureResult> CaptureSupportedAsync(ScreenRect? region, ScreenReadOptions options);
}

public interface IX11ScreenCaptureSupportProbe
{
    X11ScreenCaptureSupportResult ProbeSupport();
}

public sealed class X11ScreenCaptureFrame : IDisposable
{
    private readonly IDisposable? _owner;
    private bool _disposed;

    public X11ScreenCaptureFrame(
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
            throw new ArgumentOutOfRangeException(nameof(stride), stride, "X11 frame stride is smaller than its logical row width.");
        }

        var minimumLength = checked(stride * logicalBounds.Height);
        if (pixels.Length < minimumLength)
        {
            throw new ArgumentException("X11 frame pixel memory is smaller than the declared frame dimensions.", nameof(pixels));
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

public readonly record struct X11ScreenCaptureSupportResult
{
    private X11ScreenCaptureSupportResult(bool isSupported, ScreenReadErrorKind? errorKind, string? errorMessage)
    {
        if (!isSupported && string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new ArgumentException("Unavailable X11 screen capture probes require a message.", nameof(errorMessage));
        }

        IsSupported = isSupported;
        ErrorKind = errorKind;
        ErrorMessage = errorMessage;
    }

    public bool IsSupported { get; }

    public ScreenReadErrorKind? ErrorKind { get; }

    public string? ErrorMessage { get; }

    public static X11ScreenCaptureSupportResult Supported() => new(true, null, null);

    public static X11ScreenCaptureSupportResult Unsupported(string errorMessage) =>
        new(false, ScreenReadErrorKind.BackendUnavailable, errorMessage);

    public static X11ScreenCaptureSupportResult Failure(ScreenReadErrorKind errorKind, string errorMessage) =>
        new(false, errorKind, errorMessage);
}

public readonly record struct X11ScreenCaptureResult
{
    private X11ScreenCaptureResult(X11ScreenCaptureFrame? frame, ScreenReadErrorKind? errorKind, string? errorMessage)
    {
        if (frame is null && string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new ArgumentException("Failed X11 captures require a message.", nameof(errorMessage));
        }

        Frame = frame;
        ErrorKind = errorKind;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess => ErrorKind is null;

    public X11ScreenCaptureFrame? Frame { get; }

    public ScreenReadErrorKind? ErrorKind { get; }

    public string? ErrorMessage { get; }

    public static X11ScreenCaptureResult Success(X11ScreenCaptureFrame frame) =>
        new(frame ?? throw new ArgumentNullException(nameof(frame)), null, null);

    public static X11ScreenCaptureResult Failure(ScreenReadErrorKind errorKind, string errorMessage) =>
        new(null, errorKind, errorMessage);
}
