
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public interface IPortalPipeWireFrameCapture : IDisposable
{
    public Task<PortalPipeWireFrameResult> CaptureFrameAsync(ScreenReadOptions options);
}
