
namespace CrossMacro.Platform.Linux.Services.ScreenReading;

public sealed class KWinScreenShotScreenFrameProvider(IKWinScreenShotCapture capture, KWinScreenShotSupportResult support) : IScreenFrameProvider
{
    private readonly IKWinScreenShotCapture _capture = capture ?? throw new ArgumentNullException(nameof(capture));
    private readonly KWinScreenShotSupportResult _support = support;
    private bool _disposed;

    public KWinScreenShotScreenFrameProvider(IKWinScreenShotCapture capture)
        : this(capture, capture?.ProbeSupport() ?? throw new ArgumentNullException(nameof(capture))) { /* Empty */ }

    public string ProviderName => "KDE KWin ScreenShot2";
    public bool IsSupported => _support.IsSupported;

    public async Task<ScreenReadResult<ScreenFrame>> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_support.IsSupported)
        {
            return ScreenReadResultFactory.Failure<ScreenFrame>(
                _support.ErrorKind ?? ScreenReadErrorKind.BackendUnavailable,
                _support.ErrorMessage ?? "KDE KWin ScreenShot2 is unavailable.");
        }

        if (options.CancellationToken.IsCancellationRequested)
        {
            return LinuxScreenFrameProviderResults.CanceledBeforeStart("KDE KWin ScreenShot2 capture was canceled before it started.");
        }

        KWinScreenShotCaptureResult captureResult;
        try
        {
            captureResult = region is { } boundedRegion
                ? await _capture.CaptureAreaAsync(boundedRegion, options).ConfigureAwait(false)
                : await _capture.CaptureWorkspaceAsync(options).ConfigureAwait(false);
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
            return ScreenReadResultFactory.Failure<ScreenFrame>(ScreenReadErrorKind.CaptureFailed, "Successful KDE KWin ScreenShot2 capture did not include a frame.");
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
