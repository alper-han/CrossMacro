namespace CrossMacro.Platform.Windows.Native;

[StructLayout(LayoutKind.Sequential)]
internal struct RawMouse
{
    public ushort Flags;
    public ushort Padding;
    public ushort ButtonFlags;
    public ushort ButtonData;
    public uint RawButtons;
    public int LastX;
    public int LastY;
    public uint ExtraInformation;
}
