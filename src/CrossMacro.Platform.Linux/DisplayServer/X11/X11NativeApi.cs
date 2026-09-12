
namespace CrossMacro.Platform.Linux.DisplayServer.X11;

internal sealed class X11NativeApi : IX11NativeApi, IX11PositionNativeApi
{
    public static X11NativeApi Instance { get; } = new();

    private X11NativeApi() { /* Empty */ }

    public IntPtr OpenDisplay(string? display) => X11Native.XOpenDisplay(display);

    public int CloseDisplay(IntPtr display) => X11Native.XCloseDisplay(display);

    public IntPtr DefaultRootWindow(IntPtr display) => X11Native.XDefaultRootWindow(display);

    public int GetGeometry(
        IntPtr display,
        IntPtr drawable,
        out IntPtr root,
        out int x,
        out int y,
        out uint width,
        out uint height,
        out uint borderWidth,
        out uint depth) =>
        X11Native.XGetGeometry(display, drawable, out root, out x, out y, out width, out height, out borderWidth, out depth);

    public IntPtr GetImage(
        IntPtr display,
        IntPtr drawable,
        int x,
        int y,
        uint width,
        uint height,
        UIntPtr planeMask,
        int format) =>
        X11Native.XGetImage(display, drawable, x, y, width, height, planeMask, format);

    public UIntPtr GetPixel(IntPtr ximage, int x, int y) => X11Native.XGetPixel(ximage, x, y);

    public int DestroyImage(IntPtr ximage) => X11Native.XDestroyImage(ximage);

    public XImage ReadImage(IntPtr ximage) => Marshal.PtrToStructure<XImage>(ximage);

    public bool QueryPointer(IntPtr display, IntPtr window, out int rootX, out int rootY)
    {
        return X11Native.XQueryPointer(
            display,
            window,
            out _,
            out _,
            out rootX,
            out rootY,
            out _,
            out _,
            out _);
    }

    public int DefaultScreen(IntPtr display) => X11Native.XDefaultScreen(display);

    public int DisplayWidth(IntPtr display, int screen) => X11Native.XDisplayWidth(display, screen);

    public int DisplayHeight(IntPtr display, int screen) => X11Native.XDisplayHeight(display, screen);
}
