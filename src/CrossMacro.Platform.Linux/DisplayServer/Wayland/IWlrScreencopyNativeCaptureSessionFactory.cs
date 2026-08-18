
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public interface IWlrScreencopyNativeCaptureSessionFactory
{
    public Task<WlrScreencopyCaptureResult> CaptureRegionAsync(ScreenRect? region, ScreenReadOptions options);
}
