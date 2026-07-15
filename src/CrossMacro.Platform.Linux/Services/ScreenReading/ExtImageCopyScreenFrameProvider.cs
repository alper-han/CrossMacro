
namespace CrossMacro.Platform.Linux.Services.ScreenReading;

public sealed class ExtImageCopyScreenFrameProvider : IScreenFrameProvider
{
    private readonly IExtImageCopyCapture _capture;
    private readonly ExtImageCopySupportResult _support;
    private bool _disposed;

    public ExtImageCopyScreenFrameProvider(IExtImageCopyCapture capture)
        : this(capture, capture?.ProbeSupport() ?? throw new ArgumentNullException(nameof(capture)))
    {
    }

    public ExtImageCopyScreenFrameProvider(IExtImageCopyCapture capture, ExtImageCopySupportResult support)
    {
        _capture = capture ?? throw new ArgumentNullException(nameof(capture));
        _support = support;
    }

    public string ProviderName => "Wayland ext-image-copy-capture-v1";

    public bool IsSupported => _support.IsSupported;

    public async Task<ScreenReadResult<ScreenFrame>> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_support.IsSupported)
        {
            return ScreenReadResultFactory.Failure<ScreenFrame>(
                _support.ErrorKind ?? ScreenReadErrorKind.BackendUnavailable,
                _support.ErrorMessage ?? "ext-image-copy-capture-v1 is unavailable.");
        }

        if (options.CancellationToken.IsCancellationRequested)
        {
            return LinuxScreenFrameProviderResults.CanceledBeforeStart("ext-image-copy-capture-v1 capture was canceled before it started.");
        }

        ExtImageCopyCaptureResult captureResult;
        try
        {
            captureResult = await _capture.CaptureSupportedAsync(region, options).ConfigureAwait(false);
        }
        catch (Exception ex) when (LinuxScreenFrameProviderResults.IsKnownCaptureException(ex))
        {
            return LinuxScreenFrameProviderResults.FromKnownCaptureException(ex, "ext-image-copy-capture-v1 capture was canceled.");
        }

        if (!captureResult.IsSuccess)
        {
            return LinuxScreenFrameProviderResults.FromCaptureFailure(
                captureResult.ErrorKind,
                captureResult.ErrorMessage,
                "ext-image-copy-capture-v1 capture failed.");
        }

        var frame = captureResult.Frame;
        if (frame is null)
        {
            return ScreenReadResultFactory.Failure<ScreenFrame>(
                ScreenReadErrorKind.CaptureFailed,
                "Successful ext-image-copy capture did not include a frame.");
        }

        if (region is null || region.Value == frame.LogicalBounds)
        {
            return LinuxScreenFrameProviderResults.CreateSharedFrame(frame.LogicalBounds, frame.Stride, frame.PixelFormat, frame.Pixels, frame, frame.ValidPixelMask, frame.ValidityIndex);
        }

        try
        {
            if (!frame.LogicalBounds.Contains(region.Value))
            {
                return ScreenReadResultFactory.Failure<ScreenFrame>(
                    ScreenReadErrorKind.OutOfBounds,
                    $"Requested region {region.Value} is outside ext-image-copy frame bounds {frame.LogicalBounds}.");
            }

            return ScreenReadResultFactory.Success<ScreenFrame>(LinuxScreenFrameProviderResults.CopyRegion(
                frame.LogicalBounds,
                frame.Stride,
                frame.PixelFormat,
                frame.Pixels,
                region.Value,
                frame.ValidPixelMask));
        }
        finally
        {
            frame.Dispose();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _capture.Dispose();
    }
}
