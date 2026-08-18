namespace CrossMacro.Platform.Linux.Native.X11;

[StructLayout(LayoutKind.Sequential)]
internal struct XSelectionClearEvent
{
    public int Type;
    public nuint Serial;
    public int SendEvent;
    public IntPtr Display;
    public nuint Window;
    public nuint Selection;
    public nuint Time;
}
