
namespace CrossMacro.Platform.Windows.Native;

[StructLayout(LayoutKind.Explicit)]
internal struct InputUnion
{
    [FieldOffset(0)]
    public MouseInput mi;
    [FieldOffset(0)]
    public KeybdInput ki;
    [FieldOffset(0)]
    public HardwareInput hi;
}
