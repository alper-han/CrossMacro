
namespace CrossMacro.Platform.Windows.Native;

[StructLayout(LayoutKind.Sequential)]
internal struct KeybdInput
{
    public ushort wVk;
    public ushort wScan;
    public uint dwFlags;
    public uint time;
    public IntPtr dwExtraInfo;
}
