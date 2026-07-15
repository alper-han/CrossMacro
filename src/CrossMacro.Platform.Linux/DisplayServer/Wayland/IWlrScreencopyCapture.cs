
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public interface IWlrScreencopyCapture : IWlrScreencopySupportProbe, IDisposable
{
    public Task<WlrScreencopyCaptureResult> CaptureRegionAsync(ScreenRect? region, ScreenReadOptions options);
}
