
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.Protocols.WlrScreencopy;

public interface IWlrScreencopyCapture : IWlrScreencopySupportProbe, IDisposable
{
    public Task<WaylandCaptureResult> CaptureRegionAsync(ScreenRect? region, ScreenReadOptions options);
}
