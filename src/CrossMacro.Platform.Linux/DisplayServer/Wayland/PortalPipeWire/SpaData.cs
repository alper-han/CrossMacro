using System.Runtime.InteropServices;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.PortalPipeWire;

[StructLayout(LayoutKind.Sequential)]
internal struct SpaData
{
    public uint Type;
    public uint Flags;
    public long Fd;
    public uint MapOffset;
    public uint MaxSize;
    public IntPtr Data;
    public IntPtr Chunk;
}
