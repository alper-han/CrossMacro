using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.DisplayServer.Wayland;

namespace CrossMacro.Platform.Linux.Services.ScreenReading;

public sealed class KWinScreenShotScreenFrameProvider : IScreenFrameProvider
{
    private readonly IKWinScreenShotCapture _capture;
    private readonly KWinScreenShotSupportResult _support;
    private bool _disposed;

    public KWinScreenShotScreenFrameProvider(IKWinScreenShotCapture capture)
        : this(capture, capture?.ProbeSupport() ?? throw new ArgumentNullException(nameof(capture)))
    {
    }

    public KWinScreenShotScreenFrameProvider(IKWinScreenShotCapture capture, KWinScreenShotSupportResult support)
    {
        _capture = capture ?? throw new ArgumentNullException(nameof(capture));
        _support = support;
    }

    public string ProviderName => "KDE KWin ScreenShot2";
    public bool IsSupported => _support.IsSupported;

    public async Task<ScreenReadResult<ScreenFrame>> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_support.IsSupported)
        {
            return ScreenReadResult<ScreenFrame>.Failure(
                _support.ErrorKind ?? ScreenReadErrorKind.BackendUnavailable,
                _support.ErrorMessage ?? "KDE KWin ScreenShot2 is unavailable.");
        }

        if (region is null)
        {
            return ScreenReadResult<ScreenFrame>.Failure(
                ScreenReadErrorKind.Unsupported,
                "KDE KWin ScreenShot2 screen reading currently requires a bounded region.");
        }

        if (options.CancellationToken.IsCancellationRequested)
        {
            return LinuxScreenFrameProviderResults.CanceledBeforeStart("KDE KWin ScreenShot2 capture was canceled before it started.");
        }

        KWinScreenShotCaptureResult captureResult;
        try
        {
            captureResult = await _capture.CaptureAreaAsync(region.Value, options).ConfigureAwait(false);
        }
        catch (Exception ex) when (LinuxScreenFrameProviderResults.IsKnownCaptureException(ex))
        {
            return LinuxScreenFrameProviderResults.FromKnownCaptureException(ex, "KDE KWin ScreenShot2 capture was canceled.");
        }

        if (!captureResult.IsSuccess)
        {
            return LinuxScreenFrameProviderResults.FromCaptureFailure(
                captureResult.ErrorKind,
                captureResult.ErrorMessage,
                "KDE KWin ScreenShot2 capture failed.");
        }

        var frame = captureResult.Frame;
        if (frame is null)
        {
            return ScreenReadResult<ScreenFrame>.Failure(ScreenReadErrorKind.CaptureFailed, "Successful KDE KWin ScreenShot2 capture did not include a frame.");
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
