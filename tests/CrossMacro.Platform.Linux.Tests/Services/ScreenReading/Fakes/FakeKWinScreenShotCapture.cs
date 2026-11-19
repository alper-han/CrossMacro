
namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading.Fakes;

internal sealed class FakeKWinScreenShotCapture(
    WaylandSupportResult support,
    WaylandCaptureResult? captureResult = null) : IKWinScreenShotCapture
{
    private readonly WaylandSupportResult _support = support;
    private readonly WaylandCaptureResult _captureResult = captureResult ?? WaylandCaptureResult.Failure(
            ScreenReadErrorKind.CaptureFailed,
            "no fake KWin ScreenShot2 frame configured");

    public int CaptureCalls { get; private set; }
    public int WorkspaceCaptureCalls { get; private set; }
    public int ProbeCalls { get; private set; }
    public int DisposeCount { get; private set; }
    public Exception? CaptureException { get; init; }
    public ScreenRect? LastRegion { get; private set; }

    public Task<WaylandSupportResult> ProbeSupportAsync(CancellationToken cancellationToken = default)
    {
        ProbeCalls++;
        return Task.FromResult(_support);
    }

    public Task<WaylandCaptureResult> CaptureAreaAsync(ScreenRect region, ScreenReadOptions options)
    {
        CaptureCalls++;
        LastRegion = region;
        if (CaptureException is not null)
        {
            return Task.FromException<WaylandCaptureResult>(CaptureException);
        }

        return Task.FromResult(_captureResult);
    }

    public Task<WaylandCaptureResult> CaptureWorkspaceAsync(ScreenReadOptions options)
    {
        WorkspaceCaptureCalls++;
        if (CaptureException is not null)
        {
            return Task.FromException<WaylandCaptureResult>(CaptureException);
        }

        return Task.FromResult(_captureResult);
    }

    public void Dispose() => DisposeCount++;
}
