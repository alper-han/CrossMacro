using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.DisplayServer.Wayland;

namespace CrossMacro.Platform.Linux.Services.ScreenReading;

public sealed class WlrScreencopyScreenFrameProvider : IScreenFrameProvider
{
    private readonly IWlrScreencopyCapture _capture;
    private readonly WlrScreencopySupportResult _support;
    private bool _disposed;

    public WlrScreencopyScreenFrameProvider(IWlrScreencopyCapture capture)
        : this(capture, capture?.ProbeSupport() ?? throw new ArgumentNullException(nameof(capture)))
    {
    }

    public WlrScreencopyScreenFrameProvider(IWlrScreencopyCapture capture, WlrScreencopySupportResult support)
    {
        _capture = capture ?? throw new ArgumentNullException(nameof(capture));
        _support = support;
    }

    public string ProviderName => "Wayland wlr-screencopy-unstable-v1";

    public bool IsSupported => _support.IsSupported;

    public async Task<ScreenReadResult<ScreenFrame>> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_support.IsSupported)
        {
            return ScreenReadResult<ScreenFrame>.Failure(
                _support.ErrorKind ?? ScreenReadErrorKind.BackendUnavailable,
                _support.ErrorMessage ?? "wlr-screencopy-unstable-v1 is unavailable.");
        }

        if (options.CancellationToken.IsCancellationRequested)
        {
            return LinuxScreenFrameProviderResults.CanceledBeforeStart("wlr-screencopy-unstable-v1 capture was canceled before it started.");
        }

        WlrScreencopyCaptureResult captureResult;
        try
        {
            captureResult = await _capture.CaptureRegionAsync(region, options).ConfigureAwait(false);
        }
        catch (Exception ex) when (LinuxScreenFrameProviderResults.IsKnownCaptureException(ex))
        {
            return LinuxScreenFrameProviderResults.FromKnownCaptureException(ex, "wlr-screencopy-unstable-v1 capture was canceled.");
        }

        if (!captureResult.IsSuccess)
        {
            return LinuxScreenFrameProviderResults.FromCaptureFailure(
                captureResult.ErrorKind,
                captureResult.ErrorMessage,
                "wlr-screencopy-unstable-v1 capture failed.");
        }

        var frame = captureResult.Frame;
        if (frame is null)
        {
            return ScreenReadResult<ScreenFrame>.Failure(
                ScreenReadErrorKind.CaptureFailed,
                "Successful wlr-screencopy capture did not include a frame.");
        }

        return LinuxScreenFrameProviderResults.CreateSharedFrame(frame.LogicalBounds, frame.Stride, frame.PixelFormat, frame.Pixels, frame);
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
