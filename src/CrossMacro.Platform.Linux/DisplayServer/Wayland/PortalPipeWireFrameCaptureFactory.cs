using CrossMacro.Platform.Linux.DisplayServer.Wayland.PortalPipeWire;
using Microsoft.Win32.SafeHandles;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class PortalPipeWireFrameCaptureFactory : IPortalPipeWireFrameCaptureFactory
{
    public static PortalPipeWireFrameCaptureFactory Instance { get; } = new();

    private PortalPipeWireFrameCaptureFactory()
    {
    }

    public static bool CanLoadPipeWire() => PipeWireLibrary.CanLoad();

    public IPortalPipeWireFrameCapture Create(SafeFileHandle pipeWireRemote, uint nodeId, int width, int height)
    {
        return new PortalPipeWireFrameCapture(pipeWireRemote, nodeId, width, height);
    }
}
