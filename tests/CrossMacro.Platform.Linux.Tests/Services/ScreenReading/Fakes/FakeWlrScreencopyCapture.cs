
namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading.Fakes;

internal sealed class FakeWlrScreencopyCapture(
    WaylandSupportResult support,
    WaylandCaptureResult? captureResult = null) : IWlrScreencopyCapture
{
    private readonly WaylandSupportResult _support = support;
    private readonly WaylandCaptureResult _captureResult = captureResult ?? WaylandCaptureResult.Failure(
            ScreenReadErrorKind.CaptureFailed,
            "no fake wlr-screencopy frame configured");

    public int CaptureCalls { get; private set; }

    public int ProbeCalls { get; private set; }

    public int DisposeCount { get; private set; }

    public Exception? CaptureException { get; init; }

    public ScreenRect? LastRegion { get; private set; }

    public TimeSpan DelayBeforeResult { get; init; }

    public TaskCompletionSource CaptureStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public WaylandSupportResult ProbeSupport()
    {
        ProbeCalls++;
        return _support;
    }

    public async Task<WaylandCaptureResult> CaptureRegionAsync(ScreenRect? region, ScreenReadOptions options)
    {
        CaptureCalls++;
        LastRegion = region;
        CaptureStarted.SetResult();
        if (DelayBeforeResult > TimeSpan.Zero)
        {
            await Task.Delay(DelayBeforeResult, options.CancellationToken)
                .WaitAsync(options.Timeout ?? Timeout.InfiniteTimeSpan, options.CancellationToken);
        }

        if (CaptureException is not null)
        {
            throw CaptureException;
        }

        return _captureResult;
    }

    public void Dispose() => DisposeCount++;
}
