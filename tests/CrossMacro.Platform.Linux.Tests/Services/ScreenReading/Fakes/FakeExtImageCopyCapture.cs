
namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading.Fakes;

internal sealed class FakeExtImageCopyCapture : IExtImageCopyCapture
{
    private readonly WaylandSupportResult _support;
    private readonly Queue<WaylandCaptureResult> _captureResults;

    public FakeExtImageCopyCapture(
        WaylandSupportResult support,
        WaylandCaptureResult? captureResult = null)
    {
        _support = support;
        _captureResults = new Queue<WaylandCaptureResult>();
        _captureResults.Enqueue(captureResult ?? WaylandCaptureResult.Failure(
            ScreenReadErrorKind.CaptureFailed,
            "no fake ext-image-copy frame configured"));
    }

    public int CaptureCalls { get; private set; }

    public int ProbeCalls { get; private set; }

    public ScreenRect? LastRegion { get; private set; }

    public int DisposeCount { get; private set; }

    public Exception? CaptureException { get; init; }

    public void EnqueueCaptureResult(WaylandCaptureResult result) => _captureResults.Enqueue(result);

    public WaylandSupportResult ProbeSupport()
    {
        ProbeCalls++;
        return _support;
    }

    public Task<WaylandCaptureResult> CaptureAsync(ScreenRect? region, ScreenReadOptions options)
    {
        return CaptureSupportedAsync(region, options);
    }

    public Task<WaylandCaptureResult> CaptureSupportedAsync(ScreenRect? region, ScreenReadOptions options)
    {
        CaptureCalls++;
        LastRegion = region;
        if (CaptureException is not null)
        {
            return Task.FromException<WaylandCaptureResult>(CaptureException);
        }

        var result = _captureResults.Count > 1 ? _captureResults.Dequeue() : _captureResults.Peek();
        return Task.FromResult(result);
    }

    public void Dispose() => DisposeCount++;
}
