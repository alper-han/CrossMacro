using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.DisplayServer.X11;

namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading.Fakes;

internal sealed class FakeX11ScreenCapture : IX11ScreenCapture
{
    private readonly X11ScreenCaptureSupportResult _support;
    private readonly X11ScreenCaptureResult _captureResult;

    public FakeX11ScreenCapture(
        X11ScreenCaptureSupportResult support,
        X11ScreenCaptureResult? captureResult = null)
    {
        _support = support;
        _captureResult = captureResult ?? X11ScreenCaptureResult.Failure(
            ScreenReadErrorKind.CaptureFailed,
            "no fake X11 frame configured");
    }

    public int CaptureCalls { get; private set; }

    public int ProbeCalls { get; private set; }

    public ScreenRect? LastRegion { get; private set; }

    public int DisposeCount { get; private set; }

    public Exception? CaptureException { get; init; }

    public X11ScreenCaptureSupportResult ProbeSupport()
    {
        ProbeCalls++;
        return _support;
    }

    public Task<X11ScreenCaptureResult> CaptureAsync(ScreenRect? region, ScreenReadOptions options) =>
        CaptureSupportedAsync(region, options);

    public Task<X11ScreenCaptureResult> CaptureSupportedAsync(ScreenRect? region, ScreenReadOptions options)
    {
        CaptureCalls++;
        LastRegion = region;
        return CaptureException is null
            ? Task.FromResult(_captureResult)
            : Task.FromException<X11ScreenCaptureResult>(CaptureException);
    }

    public void Dispose() => DisposeCount++;
}
