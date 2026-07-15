
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public interface IPortalPipeWireFrameCaptureFactory
{
    public IPortalPipeWireFrameCapture Create(SafeFileHandle pipeWireRemote, uint nodeId, int width, int height);
}
