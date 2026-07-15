
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public interface IExtImageCopyNativeCaptureSessionFactory
{
    Task<ExtImageCopyCaptureResult> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options);
}
