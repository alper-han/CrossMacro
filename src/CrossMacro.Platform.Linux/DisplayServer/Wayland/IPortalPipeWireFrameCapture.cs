
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public interface IPortalPipeWireFrameCapture : IDisposable
{
    Task<PortalPipeWireFrameResult> CaptureFrameAsync(ScreenReadOptions options);
}
