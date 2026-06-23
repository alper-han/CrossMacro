using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public interface IKWinScreenShotCapture : IKWinScreenShotSupportProbe, IDisposable
{
    Task<KWinScreenShotCaptureResult> CaptureAreaAsync(ScreenRect region, ScreenReadOptions options);
}

public interface IKWinScreenShotSupportProbe
{
    KWinScreenShotSupportResult ProbeSupport();
}

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

public readonly record struct KWinScreenShotSupportResult
{
    private KWinScreenShotSupportResult(bool isSupported, ScreenReadErrorKind? errorKind, string? errorMessage)
    {
        if (!isSupported && string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new ArgumentException("Unavailable KWin screenshot probes require a message.", nameof(errorMessage));
        }

        IsSupported = isSupported;
        ErrorKind = errorKind;
        ErrorMessage = errorMessage;
    }

    public bool IsSupported { get; }
    public ScreenReadErrorKind? ErrorKind { get; }
    public string? ErrorMessage { get; }

    public static KWinScreenShotSupportResult Supported() => new(true, null, null);
    public static KWinScreenShotSupportResult Unsupported(string errorMessage) => new(false, ScreenReadErrorKind.BackendUnavailable, errorMessage);
    public static KWinScreenShotSupportResult Failure(ScreenReadErrorKind errorKind, string errorMessage) => new(false, errorKind, errorMessage);
}

public readonly record struct KWinScreenShotCaptureResult
{
    private KWinScreenShotCaptureResult(KWinScreenShotFrame? frame, ScreenReadErrorKind? errorKind, string? errorMessage)
    {
        if (frame is null && string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new ArgumentException("Failed KWin screenshot captures require a message.", nameof(errorMessage));
        }

        Frame = frame;
        ErrorKind = errorKind;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess => ErrorKind is null;
    public KWinScreenShotFrame? Frame { get; }
    public ScreenReadErrorKind? ErrorKind { get; }
    public string? ErrorMessage { get; }

    public static KWinScreenShotCaptureResult Success(KWinScreenShotFrame frame) => new(frame ?? throw new ArgumentNullException(nameof(frame)), null, null);
    public static KWinScreenShotCaptureResult Failure(ScreenReadErrorKind errorKind, string errorMessage) => new(null, errorKind, errorMessage);
}
