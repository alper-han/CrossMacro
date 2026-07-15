
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public interface IKWinScreenShotCapture : IKWinScreenShotSupportProbe, IDisposable
{
    public Task<KWinScreenShotCaptureResult> CaptureAreaAsync(ScreenRect region, ScreenReadOptions options);
}
