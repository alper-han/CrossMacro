
namespace CrossMacro.Platform.Linux.DisplayServer.X11;

public interface IX11ScreenCapture : IX11ScreenCaptureSupportProbe, IDisposable
{
    public Task<X11ScreenCaptureResult> CaptureAsync(ScreenRect? region, ScreenReadOptions options);

    public Task<X11ScreenCaptureResult> CaptureSupportedAsync(ScreenRect? region, ScreenReadOptions options);
}
