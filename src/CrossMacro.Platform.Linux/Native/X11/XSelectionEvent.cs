namespace CrossMacro.Platform.Linux.Native.X11;

[StructLayout(LayoutKind.Sequential)]
internal struct XSelectionEvent
{
    public int Type;
    public nuint Serial;
    public int SendEvent;
    public IntPtr Display;
    public nuint Requestor;
    public nuint Selection;
    public nuint Target;
    public nuint Property;
    public nuint Time;
}
