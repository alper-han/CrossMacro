using CrossMacro.Platform.Linux.Native.X11;
using System.Runtime.InteropServices;

namespace CrossMacro.Platform.Linux.DisplayServer.X11;

internal interface IX11NativeApi
{
    IntPtr OpenDisplay(string? display);

    int CloseDisplay(IntPtr display);

    IntPtr DefaultRootWindow(IntPtr display);

    int GetGeometry(
        IntPtr display,
        IntPtr drawable,
        out IntPtr root,
        out int x,
        out int y,
        out uint width,
        out uint height,
        out uint borderWidth,
        out uint depth);

    IntPtr GetImage(
        IntPtr display,
        IntPtr drawable,
        int x,
        int y,
        uint width,
        uint height,
        UIntPtr planeMask,
        int format);

    UIntPtr GetPixel(IntPtr ximage, int x, int y);

    int DestroyImage(IntPtr ximage);

    XImage ReadImage(IntPtr ximage);
}

internal sealed class X11NativeApi : IX11NativeApi
{
    public static X11NativeApi Instance { get; } = new();

    private X11NativeApi()
    {
    }

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
}
