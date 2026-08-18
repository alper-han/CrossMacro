
namespace CrossMacro.Platform.Linux.Native.X11;

[StructLayout(LayoutKind.Sequential)]
public struct XIModifierState
{
    public int @base;
    public int latched;
    public int locked;
    public int effective;
}
