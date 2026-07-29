
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class PortalPipeWireFrameCaptureFactory : IPortalPipeWireFrameCaptureFactory
{
    public static PortalPipeWireFrameCaptureFactory Instance { get; } = new();

    private PortalPipeWireFrameCaptureFactory() { /* Empty */ }

    public static bool CanLoadPipeWire() => PipeWireLibrary.CanLoad();

    public IPortalPipeWireFrameCapture Create(SafeFileHandle pipeWireRemote, uint nodeId, int width, int height)
    {
        return new PortalPipeWireFrameCapture(pipeWireRemote, nodeId, width, height);
    }
}
