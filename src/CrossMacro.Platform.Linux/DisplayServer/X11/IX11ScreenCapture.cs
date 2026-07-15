using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.Linux.DisplayServer.X11;

public interface IX11ScreenCapture : IX11ScreenCaptureSupportProbe, IDisposable
{
    Task<X11ScreenCaptureResult> CaptureAsync(ScreenRect? region, ScreenReadOptions options);

    Task<X11ScreenCaptureResult> CaptureSupportedAsync(ScreenRect? region, ScreenReadOptions options);
}
