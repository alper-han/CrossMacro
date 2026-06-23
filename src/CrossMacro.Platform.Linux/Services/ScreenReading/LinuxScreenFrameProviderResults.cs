using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.Linux.Services.ScreenReading;

internal static class LinuxScreenFrameProviderResults
{
    public static ScreenReadResult<ScreenFrame> CanceledBeforeStart(string message) =>
        ScreenReadResult<ScreenFrame>.Failure(ScreenReadErrorKind.Canceled, message);

    public static bool IsKnownCaptureException(Exception exception) =>
        exception is OperationCanceledException or TimeoutException or InvalidOperationException or IOException or UnauthorizedAccessException;

    public static ScreenReadResult<ScreenFrame> FromKnownCaptureException(Exception exception, string canceledMessage)
    {
        return exception switch
        {
            OperationCanceledException => ScreenReadResult<ScreenFrame>.Failure(ScreenReadErrorKind.Canceled, canceledMessage),
            TimeoutException => ScreenReadResult<ScreenFrame>.Failure(ScreenReadErrorKind.CaptureTimeout, exception.Message),
            InvalidOperationException or IOException or UnauthorizedAccessException => ScreenReadResult<ScreenFrame>.Failure(ScreenReadErrorKind.CaptureFailed, exception.Message),
            _ => throw new ArgumentException("Unknown capture exception.", nameof(exception))
        };
    }

    public static ScreenReadResult<ScreenFrame> FromCaptureFailure(ScreenReadErrorKind? errorKind, string? errorMessage, string fallbackMessage) =>
        ScreenReadResult<ScreenFrame>.Failure(errorKind ?? ScreenReadErrorKind.CaptureFailed, errorMessage ?? fallbackMessage);

    public static ScreenReadResult<ScreenFrame> CreateSharedFrame(
        ScreenRect logicalBounds,
        int stride,
        ScreenPixelFormat pixelFormat,
        ReadOnlyMemory<byte> pixels,
        IDisposable owner)
    {
        try
        {
            return ScreenReadResult<ScreenFrame>.Success(new ScreenFrame(logicalBounds, stride, pixelFormat, pixels, owner));
        }
        catch (Exception ex) when (ex is ArgumentException or OverflowException)
        {
            owner.Dispose();
            return ScreenReadResult<ScreenFrame>.Failure(ScreenReadErrorKind.CaptureFailed, ex.Message);
        }
    }

    public static ScreenFrame CopyRegion(
        ScreenRect sourceBounds,
        int sourceStride,
        ScreenPixelFormat pixelFormat,
        ReadOnlyMemory<byte> sourcePixels,
        ScreenRect region)
    {
        var bytesPerPixel = ScreenFrame.GetBytesPerPixel(pixelFormat);
        var targetStride = checked(region.Width * bytesPerPixel);
        var targetPixels = new byte[checked(targetStride * region.Height)];
        var sourceX = checked(region.X - sourceBounds.X);
        var sourceY = checked(region.Y - sourceBounds.Y);
        var sourceBytes = sourcePixels.Span;

        for (var row = 0; row < region.Height; row++)
        {
            var sourceOffset = checked((sourceY + row) * sourceStride + sourceX * bytesPerPixel);
            var targetOffset = checked(row * targetStride);
            sourceBytes.Slice(sourceOffset, targetStride).CopyTo(targetPixels.AsSpan(targetOffset, targetStride));
        }

        return new ScreenFrame(region, targetStride, pixelFormat, targetPixels);
    }
}
