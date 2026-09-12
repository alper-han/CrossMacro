namespace CrossMacro.Platform.Linux.DisplayServer.X11;

internal interface IX11PositionNativeApi
{
    public IntPtr OpenDisplay(string? display);

    public int CloseDisplay(IntPtr display);

    public IntPtr DefaultRootWindow(IntPtr display);

    public bool QueryPointer(
        IntPtr display,
        IntPtr window,
        out int rootX,
        out int rootY);

    public int DefaultScreen(IntPtr display);

    public int DisplayWidth(IntPtr display, int screen);

    public int DisplayHeight(IntPtr display, int screen);
}
