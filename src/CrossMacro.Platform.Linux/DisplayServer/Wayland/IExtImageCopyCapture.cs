using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public interface IExtImageCopyCapture : IExtImageCopySupportProbe, IDisposable
{
    Task<ExtImageCopyCaptureResult> CaptureAsync(ScreenRect? region, ScreenReadOptions options);

    Task<ExtImageCopyCaptureResult> CaptureSupportedAsync(ScreenRect? region, ScreenReadOptions options);
}

public interface IExtImageCopyNativeCaptureSessionFactory
{
    Task<ExtImageCopyCaptureResult> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options);
}

public sealed class ExtImageCopyFrame : IDisposable
{
    private readonly IDisposable? _owner;
    private bool _disposed;

    public ExtImageCopyFrame(
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
            throw new ArgumentOutOfRangeException(nameof(stride), stride, "ext-image-copy frame stride is smaller than its logical row width.");
        }

        var minimumLength = checked(stride * logicalBounds.Height);
        if (pixels.Length < minimumLength)
        {
            throw new ArgumentException("ext-image-copy frame pixel memory is smaller than the declared frame dimensions.", nameof(pixels));
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

public readonly record struct ExtImageCopyCaptureResult
{
    private ExtImageCopyCaptureResult(ExtImageCopyFrame? frame, ScreenReadErrorKind? errorKind, string? errorMessage)
    {
        if (frame is null && string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new ArgumentException("Failed ext-image-copy captures require a message.", nameof(errorMessage));
        }

        Frame = frame;
        ErrorKind = errorKind;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess => ErrorKind is null;

    public ExtImageCopyFrame? Frame { get; }

    public ScreenReadErrorKind? ErrorKind { get; }

    public string? ErrorMessage { get; }

    public static ExtImageCopyCaptureResult Success(ExtImageCopyFrame frame) =>
        new(frame ?? throw new ArgumentNullException(nameof(frame)), null, null);

    public static ExtImageCopyCaptureResult Failure(ScreenReadErrorKind errorKind, string errorMessage) =>
        new(null, errorKind, errorMessage);
}
