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
        IDisposable? owner = null,
        ReadOnlyMemory<byte> validPixelMask = default,
        int? physicalWidth = null,
        int? physicalHeight = null,
        ScreenFrameValidityIndex? validityIndex = null)
    {
        var bytesPerPixel = ScreenFrame.GetBytesPerPixel(pixelFormat);
        var declaredPhysicalWidth = physicalWidth ?? logicalBounds.Width;
        var declaredPhysicalHeight = physicalHeight ?? logicalBounds.Height;
        if (declaredPhysicalWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(physicalWidth), declaredPhysicalWidth, "wlr-screencopy frame physical width must be positive.");
        }

        if (declaredPhysicalHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(physicalHeight), declaredPhysicalHeight, "wlr-screencopy frame physical height must be positive.");
        }

        var minimumStride = checked(declaredPhysicalWidth * bytesPerPixel);
        if (stride < minimumStride)
        {
            throw new ArgumentOutOfRangeException(nameof(stride), stride, "wlr-screencopy frame stride is smaller than its physical row width.");
        }

        var minimumLength = checked(stride * declaredPhysicalHeight);
        if (pixels.Length < minimumLength)
        {
            throw new ArgumentException("wlr-screencopy frame pixel memory is smaller than the declared physical frame dimensions.", nameof(pixels));
        }

        var validPixelCount = checked(logicalBounds.Width * logicalBounds.Height);
        if (!validPixelMask.IsEmpty && validPixelMask.Length < validPixelCount)
        {
            throw new ArgumentException("wlr-screencopy frame valid-pixel mask is smaller than the declared frame dimensions.", nameof(validPixelMask));
        }

        LogicalBounds = logicalBounds;
        PhysicalWidth = declaredPhysicalWidth;
        PhysicalHeight = declaredPhysicalHeight;
        Stride = stride;
        PixelFormat = pixelFormat;
        Pixels = pixels;
        ValidPixelMask = validPixelMask.IsEmpty ? ReadOnlyMemory<byte>.Empty : validPixelMask.Slice(0, validPixelCount);
        _owner = owner;
        ValidityIndex = validityIndex;
    }

    public ScreenRect LogicalBounds { get; }

    public int PhysicalWidth { get; }

    public int PhysicalHeight { get; }

    public int Stride { get; }

    public ScreenPixelFormat PixelFormat { get; }

    public ReadOnlyMemory<byte> Pixels { get; }

    public ReadOnlyMemory<byte> ValidPixelMask { get; }

    public ScreenFrameValidityIndex? ValidityIndex { get; }

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
        new(frame ?? throw new ArgumentNullException(nameof(frame)), errorKind: null, errorMessage: null);

    public static WlrScreencopyCaptureResult Failure(ScreenReadErrorKind errorKind, string errorMessage) =>
        new(frame: null, errorKind, errorMessage);
}
