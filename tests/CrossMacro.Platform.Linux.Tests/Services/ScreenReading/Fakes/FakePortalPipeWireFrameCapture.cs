
namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading.Fakes;

internal sealed class FakePortalPipeWireFrameCapture(PortalPipeWireFrameResult result) : IPortalPipeWireFrameCapture
{
    private readonly PortalPipeWireFrameResult _result = result;

    public int CaptureCalls { get; private set; }

    public int DisposeCount { get; private set; }

    public Exception? CaptureException { get; init; }

    public Queue<PortalPipeWireFrameResult>? CaptureResultSequence { get; init; }

    public TaskCompletionSource<PortalPipeWireFrameResult>? PendingCapture { get; init; }

    public Action? CaptureStarted { get; init; }

    public Func<ScreenReadOptions, Task<PortalPipeWireFrameResult>>? CaptureHandler { get; init; }

    public ScreenRect? LastRegion { get; private set; }

    public List<ScreenReadOptions> Options { get; } = [];

    public Task<PortalPipeWireFrameResult> CaptureFrameAsync(ScreenReadOptions options)
        => Capture(options);

    public Task<PortalPipeWireFrameResult> CaptureFrameAsync(ScreenRect region, ScreenReadOptions options)
    {
        LastRegion = region;
        return Capture(options);
    }

    private Task<PortalPipeWireFrameResult> Capture(ScreenReadOptions options)
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
            return Task.FromException<PortalPipeWireFrameResult>(CaptureException);
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
