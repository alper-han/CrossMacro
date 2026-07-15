
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public interface IExtImageCopyNativeCaptureSessionFactory
{
    public Task<ExtImageCopyCaptureResult> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options);
}
