
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.Protocols.ExtImageCopy;

public interface IExtImageCopyNativeCaptureSessionFactory
{
    public Task<WaylandCaptureResult> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options);
}
