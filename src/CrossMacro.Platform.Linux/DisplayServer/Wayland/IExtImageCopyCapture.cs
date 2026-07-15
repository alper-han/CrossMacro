using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public interface IExtImageCopyCapture : IExtImageCopySupportProbe, IDisposable
{
    Task<ExtImageCopyCaptureResult> CaptureAsync(ScreenRect? region, ScreenReadOptions options);

    Task<ExtImageCopyCaptureResult> CaptureSupportedAsync(ScreenRect? region, ScreenReadOptions options);
}
