
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public interface IPortalPipeWireFrameCapture : IDisposable
{
    public Task<PortalPipeWireFrameResult> CaptureFrameAsync(ScreenReadOptions options);

    public Task<PortalPipeWireFrameResult> CaptureFrameAsync(ScreenRect region, ScreenReadOptions options) => CaptureFrameAsync(options);
}
