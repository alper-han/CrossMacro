
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.Kde;

public interface IKWinScreenShotCapture : IKWinScreenShotSupportProbe, IDisposable
{
    public Task<WaylandCaptureResult> CaptureAreaAsync(ScreenRect region, ScreenReadOptions options);

    public Task<WaylandCaptureResult> CaptureWorkspaceAsync(ScreenReadOptions options);
}
