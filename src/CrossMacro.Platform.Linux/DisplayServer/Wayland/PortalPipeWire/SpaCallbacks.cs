using System.Runtime.InteropServices;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.PortalPipeWire;

[StructLayout(LayoutKind.Sequential)]
internal struct SpaCallbacks
{
    public IntPtr Functions;
    public IntPtr Data;
}
