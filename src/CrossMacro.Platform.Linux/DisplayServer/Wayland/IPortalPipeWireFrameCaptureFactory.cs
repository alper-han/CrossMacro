using CrossMacro.Platform.Abstractions;
using Microsoft.Win32.SafeHandles;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public interface IPortalPipeWireFrameCaptureFactory
{
    IPortalPipeWireFrameCapture Create(SafeFileHandle pipeWireRemote, uint nodeId, int width, int height);
}
