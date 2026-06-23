using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.DisplayServer.X11;

namespace CrossMacro.Platform.Linux.Services.ScreenReading;

public sealed class X11ScreenFrameProvider : IScreenFrameProvider
{
    private readonly IX11ScreenCapture _capture;
    private readonly X11ScreenCaptureSupportResult _support;
    private bool _disposed;

    public X11ScreenFrameProvider(IX11ScreenCapture capture)
        : this(capture, capture?.ProbeSupport() ?? throw new ArgumentNullException(nameof(capture)))
    {
    }

    public X11ScreenFrameProvider(IX11ScreenCapture capture, X11ScreenCaptureSupportResult support)
    {
        _capture = capture ?? throw new ArgumentNullException(nameof(capture));
        _support = support;
    }

    public string ProviderName => "X11 XGetImage";

    public bool IsSupported => _support.IsSupported;

    public async Task<ScreenReadResult<ScreenFrame>> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_support.IsSupported)
        {
            return ScreenReadResult<ScreenFrame>.Failure(
                _support.ErrorKind ?? ScreenReadErrorKind.BackendUnavailable,
                _support.ErrorMessage ?? "X11 screen reading is unavailable.");
        }

        if (options.CancellationToken.IsCancellationRequested)
        {
            return LinuxScreenFrameProviderResults.CanceledBeforeStart("X11 screen capture was canceled before it started.");
        }

        X11ScreenCaptureResult captureResult;
        try
        {
            captureResult = await _capture.CaptureSupportedAsync(region, options).ConfigureAwait(false);
        }
        catch (Exception ex) when (LinuxScreenFrameProviderResults.IsKnownCaptureException(ex))
        {
            return LinuxScreenFrameProviderResults.FromKnownCaptureException(ex, "X11 screen capture was canceled.");
        }

        if (!captureResult.IsSuccess)
        {
            return LinuxScreenFrameProviderResults.FromCaptureFailure(
                captureResult.ErrorKind,
                captureResult.ErrorMessage,
                "X11 screen capture failed.");
        }

        var frame = captureResult.Frame;
        if (frame is null)
        {
            return ScreenReadResult<ScreenFrame>.Failure(
                ScreenReadErrorKind.CaptureFailed,
                "Successful X11 screen capture did not include a frame.");
        }

        return LinuxScreenFrameProviderResults.CreateSharedFrame(
            frame.LogicalBounds,
            frame.Stride,
            frame.PixelFormat,
            frame.Pixels,
            frame);
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
