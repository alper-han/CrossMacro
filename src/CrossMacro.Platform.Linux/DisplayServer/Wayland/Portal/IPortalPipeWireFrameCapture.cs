
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.Portal;

public interface IPortalPipeWireFrameCapture : IDisposable
{
    public Task<WaylandCaptureResult> CaptureFrameAsync(ScreenReadOptions options);

    public Task<WaylandCaptureResult> CaptureFrameAsync(ScreenRect region, ScreenReadOptions options) => CaptureFrameAsync(options);
}
