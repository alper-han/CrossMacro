
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public interface IPortalScreenCastCapture : IPortalScreenCastSupportProbe, IDisposable
{
    Task<PortalScreenCastCaptureResult> CaptureAsync(ScreenReadOptions options);

    Task<PortalScreenCastCaptureResult> CaptureSupportedAsync(ScreenReadOptions options);

    Task<PortalScreenCastCaptureResult> CaptureSupportedAsync(ScreenRect? region, ScreenReadOptions options);
}
