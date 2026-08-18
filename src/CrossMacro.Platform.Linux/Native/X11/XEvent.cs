
namespace CrossMacro.Platform.Linux.Native.X11;

[StructLayout(LayoutKind.Explicit, Size = 192)]
public struct XEvent
{
    [FieldOffset(0)] public int type;
    [FieldOffset(0)] public XGenericEventCookie xcookie;
}
