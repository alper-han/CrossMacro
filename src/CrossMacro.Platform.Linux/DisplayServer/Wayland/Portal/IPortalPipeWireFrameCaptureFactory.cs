
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.Portal;

public interface IPortalPipeWireFrameCaptureFactory
{
    public IPortalPipeWireFrameCapture Create(SafeFileHandle pipeWireRemote, uint nodeId, int width, int height);

    public IPortalPipeWireFrameCapture Create(
        SafeFileHandle pipeWireRemote,
        PortalStreamDescriptor stream,
        int width,
        int height) => Create(pipeWireRemote, stream.NodeId, width, height);
}
