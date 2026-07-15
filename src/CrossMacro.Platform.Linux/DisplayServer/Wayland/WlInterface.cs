using System.Runtime.InteropServices;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

[StructLayout(LayoutKind.Sequential)]
internal struct WlInterface
{
    public IntPtr Name;
    public int Version;
    public int MethodCount;
    public IntPtr Methods;
    public int EventCount;
    public IntPtr Events;
}
