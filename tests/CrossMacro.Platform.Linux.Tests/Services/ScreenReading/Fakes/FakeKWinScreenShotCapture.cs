
namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading.Fakes;

internal sealed class FakeKWinScreenShotCapture(
    KWinScreenShotSupportResult support,
    KWinScreenShotCaptureResult? captureResult = null) : IKWinScreenShotCapture
{
    private readonly KWinScreenShotSupportResult _support = support;
    private readonly KWinScreenShotCaptureResult _captureResult = captureResult ?? KWinScreenShotCaptureResult.Failure(
            ScreenReadErrorKind.CaptureFailed,
            "no fake KWin ScreenShot2 frame configured");

    public int CaptureCalls { get; private set; }
    public int WorkspaceCaptureCalls { get; private set; }
    public int ProbeCalls { get; private set; }
    public int DisposeCount { get; private set; }
    public Exception? CaptureException { get; init; }
    public ScreenRect? LastRegion { get; private set; }

    public KWinScreenShotSupportResult ProbeSupport()
    {
        ProbeCalls++;
        return _support;
    }

    public Task<KWinScreenShotCaptureResult> CaptureAreaAsync(ScreenRect region, ScreenReadOptions options)
    {
        CaptureCalls++;
        LastRegion = region;
        if (CaptureException is not null)
        {
            return Task.FromException<KWinScreenShotCaptureResult>(CaptureException);
        }

        return Task.FromResult(_captureResult);
    }

    public Task<KWinScreenShotCaptureResult> CaptureWorkspaceAsync(ScreenReadOptions options)
    {
        WorkspaceCaptureCalls++;
        if (CaptureException is not null)
        {
            return Task.FromException<KWinScreenShotCaptureResult>(CaptureException);
        }

        return Task.FromResult(_captureResult);
    }

    public void Dispose() => DisposeCount++;
}
