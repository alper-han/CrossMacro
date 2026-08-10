
namespace CrossMacro.Platform.Linux.Services.ScreenReading;

internal static class LinuxScreenFrameProviderResults
{
    public static ScreenReadResult<ScreenFrame> CanceledBeforeStart(string message) =>
        ScreenReadResultFactory.Failure<ScreenFrame>(ScreenReadErrorKind.Canceled, message);

    public static bool IsKnownCaptureException(Exception exception) =>
        exception is OperationCanceledException or TimeoutException or InvalidOperationException or IOException or UnauthorizedAccessException;

    public static ScreenReadResult<ScreenFrame> FromKnownCaptureException(Exception exception, string canceledMessage)
    {
        return exception switch
        {
            OperationCanceledException => ScreenReadResultFactory.Failure<ScreenFrame>(ScreenReadErrorKind.Canceled, canceledMessage),
            TimeoutException => ScreenReadResultFactory.Failure<ScreenFrame>(ScreenReadErrorKind.CaptureTimeout, exception.Message),
            InvalidOperationException or IOException or UnauthorizedAccessException => ScreenReadResultFactory.Failure<ScreenFrame>(ScreenReadErrorKind.CaptureFailed, exception.Message),
            _ => throw new ArgumentException("Unknown capture exception.", nameof(exception)),
        };
    }

    public static ScreenReadResult<ScreenFrame> FromCaptureFailure(ScreenReadErrorKind? errorKind, string? errorMessage, string fallbackMessage) =>
        ScreenReadResultFactory.Failure<ScreenFrame>(errorKind ?? ScreenReadErrorKind.CaptureFailed, errorMessage ?? fallbackMessage);

    public static ScreenReadResult<ScreenFrame> CreateSharedFrame(
        ScreenRect logicalBounds,
        int stride,
        ScreenPixelFormat pixelFormat,
        ReadOnlyMemory<byte> pixels,
        IDisposable owner,
        ReadOnlyMemory<byte> validPixelMask = default,
        ScreenFrameValidityIndex? validityIndex = null,
        ScreenAlphaMode alphaMode = ScreenAlphaMode.Opaque)
    {
        try
        {
            return ScreenReadResultFactory.Success<ScreenFrame>(new ScreenFrame(
                logicalBounds,
                stride,
                pixelFormat,
                pixels,
                owner,
                validPixelMask,
                validityIndex,
                alphaMode));
        }
        catch (Exception ex) when (ex is ArgumentException or OverflowException)
        {
            owner.Dispose();
            return ScreenReadResultFactory.Failure<ScreenFrame>(ScreenReadErrorKind.CaptureFailed, ex.Message);
        }
    }

    public static ScreenFrame CopyRegion(
        ScreenRect sourceBounds,
        int sourceStride,
        ScreenPixelFormat pixelFormat,
        ReadOnlyMemory<byte> sourcePixels,
        ScreenRect region,
        ReadOnlyMemory<byte> sourceValidPixelMask = default)
    {
        var bytesPerPixel = ScreenFrame.GetBytesPerPixel(pixelFormat);
        var targetStride = checked(region.Width * bytesPerPixel);
        var targetPixels = new byte[checked(targetStride * region.Height)];
        var sourceX = checked(region.X - sourceBounds.X);
        var sourceY = checked(region.Y - sourceBounds.Y);
        var sourceBytes = sourcePixels.Span;
        byte[]? targetValidPixelMask = sourceValidPixelMask.IsEmpty ? null : new byte[checked(region.Width * region.Height)];
        var sourceMask = sourceValidPixelMask.Span;

        for (var row = 0; row < region.Height; row++)
        {
            var sourceOffset = checked(((sourceY + row) * sourceStride) + (sourceX * bytesPerPixel));
            var targetOffset = checked(row * targetStride);
            sourceBytes.Slice(sourceOffset, targetStride).CopyTo(targetPixels.AsSpan(targetOffset, targetStride));

            if (targetValidPixelMask is not null)
            {
                var sourceMaskOffset = checked(((sourceY + row) * sourceBounds.Width) + sourceX);
                var targetMaskOffset = checked(row * region.Width);
                sourceMask.Slice(sourceMaskOffset, region.Width).CopyTo(targetValidPixelMask.AsSpan(targetMaskOffset, region.Width));
            }
        }

        var targetMask = targetValidPixelMask is null ? ReadOnlyMemory<byte>.Empty : targetValidPixelMask;
        return new ScreenFrame(region, targetStride, pixelFormat, targetPixels, validPixelMask: targetMask, alphaMode: ScreenAlphaMode.Opaque);
    }
}
