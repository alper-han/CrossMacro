
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.Portal;

public interface IPortalScreenCastCapture : IPortalScreenCastSupportProbe, IDisposable
{
    public Task<WaylandCaptureResult> CaptureAsync(ScreenReadOptions options);

    public Task<WaylandCaptureResult> CaptureSupportedAsync(ScreenReadOptions options);

    public Task<WaylandCaptureResult> CaptureSupportedAsync(ScreenRect? region, ScreenReadOptions options);
}
