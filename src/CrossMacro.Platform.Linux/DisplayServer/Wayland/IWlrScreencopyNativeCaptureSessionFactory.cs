
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public interface IWlrScreencopyNativeCaptureSessionFactory
{
    Task<WlrScreencopyCaptureResult> CaptureRegionAsync(ScreenRect? region, ScreenReadOptions options);
}
