
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

[StructLayout(LayoutKind.Explicit)]
internal struct WlArgument
{
    [FieldOffset(0)] public int i;
    [FieldOffset(0)] public uint u;
    [FieldOffset(0)] public IntPtr s;
    [FieldOffset(0)] public IntPtr o;
    [FieldOffset(0)] public int h;
}
