using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.DisplayServer.Wayland;

namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading.Fakes;

internal sealed class FakeWlrScreencopyCapture : IWlrScreencopyCapture
{
    private readonly WlrScreencopySupportResult _support;
    private readonly WlrScreencopyCaptureResult _captureResult;

    public FakeWlrScreencopyCapture(
        WlrScreencopySupportResult support,
        WlrScreencopyCaptureResult? captureResult = null)
    {
        _support = support;
        _captureResult = captureResult ?? WlrScreencopyCaptureResult.Failure(
            ScreenReadErrorKind.CaptureFailed,
            "no fake wlr-screencopy frame configured");
    }

    public int CaptureCalls { get; private set; }

    public int ProbeCalls { get; private set; }

    public int DisposeCount { get; private set; }

    public Exception? CaptureException { get; init; }

    public ScreenRect? LastRegion { get; private set; }

    public WlrScreencopySupportResult ProbeSupport()
    {
        ProbeCalls++;
        return _support;
    }

    public Task<WlrScreencopyCaptureResult> CaptureRegionAsync(ScreenRect? region, ScreenReadOptions options)
    {
        CaptureCalls++;
        LastRegion = region;
        if (CaptureException is not null)
        {
            return Task.FromException<WlrScreencopyCaptureResult>(CaptureException);
        }

        return Task.FromResult(_captureResult);
    }

    public void Dispose() => DisposeCount++;
}
