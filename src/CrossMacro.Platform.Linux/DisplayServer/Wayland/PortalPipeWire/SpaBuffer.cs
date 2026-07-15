using System.Runtime.InteropServices;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.PortalPipeWire;

[StructLayout(LayoutKind.Sequential)]
internal struct SpaBuffer
{
    public uint MetaCount;
    public uint DataCount;
    public IntPtr Metas;
    public IntPtr Datas;
}
