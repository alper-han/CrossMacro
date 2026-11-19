
namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading.Fakes;

internal sealed class FakePortalPipeWireFrameCapture(WaylandCaptureResult result) : IPortalPipeWireFrameCapture
{
    private readonly WaylandCaptureResult _result = result;

    public int CaptureCalls { get; private set; }

    public int DisposeCount { get; private set; }

    public Exception? CaptureException { get; init; }

    public Queue<WaylandCaptureResult>? CaptureResultSequence { get; init; }

    public TaskCompletionSource<WaylandCaptureResult>? PendingCapture { get; init; }

    public Action? CaptureStarted { get; init; }

    public Func<ScreenReadOptions, Task<WaylandCaptureResult>>? CaptureHandler { get; init; }

    public ScreenRect? LastRegion { get; private set; }

    public List<ScreenReadOptions> Options { get; } = [];

    public Task<WaylandCaptureResult> CaptureFrameAsync(ScreenReadOptions options)
        => CaptureAsync(options);

    public Task<WaylandCaptureResult> CaptureFrameAsync(ScreenRect region, ScreenReadOptions options)
    {
        LastRegion = region;
        return CaptureAsync(options);
    }

    private Task<WaylandCaptureResult> CaptureAsync(ScreenReadOptions options)
    {
        CaptureCalls++;
        Options.Add(options);
        CaptureStarted?.Invoke();
        if (CaptureHandler is not null)
        {
            return CaptureHandler(options);
        }

        if (CaptureException is not null)
        {
            return Task.FromException<WaylandCaptureResult>(CaptureException);
        }

        if (PendingCapture is not null)
        {
            return PendingCapture.Task.WaitAsync(options.CancellationToken);
        }

        if (CaptureResultSequence is { Count: > 0 })
        {
            return Task.FromResult(CaptureResultSequence.Dequeue());
        }

        return Task.FromResult(_result);
    }

    public void Dispose() => DisposeCount++;
}
