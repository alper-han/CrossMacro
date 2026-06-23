using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.DisplayServer.Wayland;

namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading.Fakes;

internal sealed class FakePortalScreenCastCapture : IPortalScreenCastCapture
{
    private readonly PortalScreenCastSupportResult _support;
    private readonly PortalScreenCastCaptureResult _captureResult;

    public FakePortalScreenCastCapture(
        PortalScreenCastSupportResult support,
        PortalScreenCastCaptureResult? captureResult = null)
    {
        _support = support;
        _captureResult = captureResult ?? PortalScreenCastCaptureResult.Failure(
            ScreenReadErrorKind.CaptureFailed,
            "no fake portal frame configured");
    }

    public int CaptureCalls { get; private set; }

    public ScreenRect? LastRegion { get; private set; }

    public int ProbeCalls { get; private set; }

    public int DisposeCount { get; private set; }

    public Exception? CaptureException { get; init; }

    public PortalScreenCastSupportResult ProbeSupport()
    {
        ProbeCalls++;
        return _support;
    }

    public Task<PortalScreenCastCaptureResult> CaptureAsync(ScreenReadOptions options)
    {
        return CaptureSupportedAsync(options);
    }

    public Task<PortalScreenCastCaptureResult> CaptureSupportedAsync(ScreenReadOptions options)
    {
        return CaptureSupportedAsync(null, options);
    }

    public Task<PortalScreenCastCaptureResult> CaptureSupportedAsync(ScreenRect? region, ScreenReadOptions options)
    {
        CaptureCalls++;
        LastRegion = region;
        if (CaptureException is not null)
        {
            return Task.FromException<PortalScreenCastCaptureResult>(CaptureException);
        }

        return Task.FromResult(_captureResult);
    }

    public void Dispose() => DisposeCount++;
}
