
namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading.Fakes;

internal sealed class FakePortalPipeWireFrameCapture(PortalPipeWireFrameResult result) : IPortalPipeWireFrameCapture
{
    private readonly PortalPipeWireFrameResult _result = result;

    public int CaptureCalls { get; private set; }

    public int DisposeCount { get; private set; }

    public Exception? CaptureException { get; init; }

    public TaskCompletionSource<PortalPipeWireFrameResult>? PendingCapture { get; init; }

    public Action? CaptureStarted { get; init; }

    public ScreenRect? LastRegion { get; private set; }

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
        CaptureStarted?.Invoke();
        if (CaptureException is not null)
        {
            return Task.FromException<PortalPipeWireFrameResult>(CaptureException);
        }

        if (PendingCapture is not null)
        {
            return PendingCapture.Task.WaitAsync(options.CancellationToken);
        }

        return Task.FromResult(_result);
    }

    public void Dispose() => DisposeCount++;
}
