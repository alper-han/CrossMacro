using System.Runtime.InteropServices;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.PortalPipeWire;

[StructLayout(LayoutKind.Sequential)]
internal struct PipeWireBuffer
{
    public IntPtr Buffer;
    public IntPtr UserData;
    public ulong Size;
    public ulong Requested;
    public ulong Time;
}
