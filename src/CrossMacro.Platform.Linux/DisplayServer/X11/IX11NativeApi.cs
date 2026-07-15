
namespace CrossMacro.Platform.Linux.DisplayServer.X11;

internal interface IX11NativeApi
{
    public IntPtr OpenDisplay(string? display);

    public int CloseDisplay(IntPtr display);

    public IntPtr DefaultRootWindow(IntPtr display);

    public int GetGeometry(
        IntPtr display,
        IntPtr drawable,
        out IntPtr root,
        out int x,
        out int y,
        out uint width,
        out uint height,
        out uint borderWidth,
        out uint depth);

    public IntPtr GetImage(
        IntPtr display,
        IntPtr drawable,
        int x,
        int y,
        uint width,
        uint height,
        UIntPtr planeMask,
        int format);

    public UIntPtr GetPixel(IntPtr ximage, int x, int y);

    public int DestroyImage(IntPtr ximage);

    public XImage ReadImage(IntPtr ximage);
}
