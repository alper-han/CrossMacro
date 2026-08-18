
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

[StructLayout(LayoutKind.Sequential)]
internal struct WlMessage
{
    public IntPtr Name;
    public IntPtr Signature;
    public IntPtr Types;
}
