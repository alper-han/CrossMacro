
namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading.Fakes;

internal sealed class FakePortalPipeWireFrameCapture(PortalPipeWireFrameResult result) : IPortalPipeWireFrameCapture
{
    private readonly PortalPipeWireFrameResult _result = result;

    public int CaptureCalls { get; private set; }

    public int DisposeCount { get; private set; }

    public Exception? CaptureException { get; init; }

    public Task<PortalPipeWireFrameResult> CaptureFrameAsync(ScreenReadOptions options)
    {
        CaptureCalls++;
        if (CaptureException is not null)
        {
            return Task.FromException<PortalPipeWireFrameResult>(CaptureException);
        }

        return Task.FromResult(_result);
    }

    public void Dispose() => DisposeCount++;
}
