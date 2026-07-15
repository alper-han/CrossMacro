using System.Runtime.InteropServices;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.PortalPipeWire;

[StructLayout(LayoutKind.Sequential)]
internal struct SpaChunk
{
    public uint Offset;
    public uint Size;
    public int Stride;
    public int Flags;
}
