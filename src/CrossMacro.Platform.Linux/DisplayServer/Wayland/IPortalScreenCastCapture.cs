
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public interface IPortalScreenCastCapture : IPortalScreenCastSupportProbe, IDisposable
{
    public Task<PortalScreenCastCaptureResult> CaptureAsync(ScreenReadOptions options);

    public Task<PortalScreenCastCaptureResult> CaptureSupportedAsync(ScreenReadOptions options);

    public Task<PortalScreenCastCaptureResult> CaptureSupportedAsync(ScreenRect? region, ScreenReadOptions options);
}
