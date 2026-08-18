namespace CrossMacro.Platform.Linux.Native.X11;

[StructLayout(LayoutKind.Sequential)]
internal struct XPropertyEvent
{
    public int Type;
    public nuint Serial;
    public int SendEvent;
    public IntPtr Display;
    public nuint Window;
    public nuint Atom;
    public nuint Time;
    public int State;
}
