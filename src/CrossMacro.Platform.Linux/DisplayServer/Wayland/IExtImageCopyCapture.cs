
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public interface IExtImageCopyCapture : IExtImageCopySupportProbe, IDisposable
{
    public Task<ExtImageCopyCaptureResult> CaptureAsync(ScreenRect? region, ScreenReadOptions options);

    public Task<ExtImageCopyCaptureResult> CaptureSupportedAsync(ScreenRect? region, ScreenReadOptions options);
}
