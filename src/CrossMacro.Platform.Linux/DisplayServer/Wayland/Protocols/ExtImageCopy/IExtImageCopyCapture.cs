
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.Protocols.ExtImageCopy;

public interface IExtImageCopyCapture : IExtImageCopySupportProbe, IDisposable
{
    public Task<WaylandCaptureResult> CaptureAsync(ScreenRect? region, ScreenReadOptions options);

    public Task<WaylandCaptureResult> CaptureSupportedAsync(ScreenRect? region, ScreenReadOptions options);
}
