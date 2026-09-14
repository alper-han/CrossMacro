
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.Protocols.WlrScreencopy;

public interface IWlrScreencopyCapture : IWlrScreencopySupportProbe, IDisposable
{
    public Task<WlrScreencopyCaptureResult> CaptureRegionAsync(ScreenRect? region, ScreenReadOptions options);
}
