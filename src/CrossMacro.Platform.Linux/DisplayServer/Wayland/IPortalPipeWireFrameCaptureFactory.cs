
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public interface IPortalPipeWireFrameCaptureFactory
{
    IPortalPipeWireFrameCapture Create(SafeFileHandle pipeWireRemote, uint nodeId, int width, int height);
}
