
namespace CrossMacro.Platform.Windows.Native;

[StructLayout(LayoutKind.Sequential)]
internal struct InputStruct
{
    public uint type;
    public InputUnion U;
    public static int Size => Marshal.SizeOf<InputStruct>();
}
