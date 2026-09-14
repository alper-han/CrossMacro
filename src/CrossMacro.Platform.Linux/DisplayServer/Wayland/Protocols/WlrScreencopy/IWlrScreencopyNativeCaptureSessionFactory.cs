
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.Protocols.WlrScreencopy;

public interface IWlrScreencopyNativeCaptureSessionFactory
{
    public Task<WlrScreencopyCaptureResult> CaptureRegionAsync(ScreenRect? region, ScreenReadOptions options);
}
