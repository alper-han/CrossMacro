
namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading.Fakes;

internal sealed class FakePortalScreenCastCapture(
    WaylandSupportResult support,
    WaylandCaptureResult? captureResult = null) : IPortalScreenCastCapture
{
    private readonly WaylandSupportResult _support = support;
    private readonly WaylandCaptureResult _captureResult = captureResult ?? WaylandCaptureResult.Failure(
            ScreenReadErrorKind.CaptureFailed,
            "no fake portal frame configured");

    public int CaptureCalls { get; private set; }

    public ScreenRect? LastRegion { get; private set; }

    public int ProbeCalls { get; private set; }

    public int DisposeCount { get; private set; }

    public Exception? CaptureException { get; init; }

    public WaylandSupportResult ProbeSupport()
    {
        ProbeCalls++;
        return _support;
    }

    public Task<WaylandCaptureResult> CaptureAsync(ScreenReadOptions options)
    {
        return CaptureSupportedAsync(options);
    }

    public Task<WaylandCaptureResult> CaptureSupportedAsync(ScreenReadOptions options)
    {
        return CaptureSupportedAsync(region: null, options);
    }

    public Task<WaylandCaptureResult> CaptureSupportedAsync(ScreenRect? region, ScreenReadOptions options)
    {
        CaptureCalls++;
        LastRegion = region;
        if (CaptureException is not null)
        {
            return Task.FromException<WaylandCaptureResult>(CaptureException);
        }

        return Task.FromResult(_captureResult);
    }

    public void Dispose() => DisposeCount++;
}
