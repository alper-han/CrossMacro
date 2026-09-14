
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.Kde;

public interface IKWinScreenShotCapture : IKWinScreenShotSupportProbe, IDisposable
{
    public Task<KWinScreenShotCaptureResult> CaptureAreaAsync(ScreenRect region, ScreenReadOptions options);

    public Task<KWinScreenShotCaptureResult> CaptureWorkspaceAsync(ScreenReadOptions options);
}
