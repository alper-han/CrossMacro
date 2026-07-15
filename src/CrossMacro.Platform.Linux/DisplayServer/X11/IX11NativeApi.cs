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
