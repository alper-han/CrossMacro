using System.Runtime.InteropServices;

namespace CrossMacro.Platform.Windows.Native;

internal static class Dwmapi
{
    internal const uint DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    internal const uint DWMWA_CLOAKED = 14;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    internal static extern int DwmGetWindowAttribute(
        IntPtr hwnd,
        uint dwAttribute,
        out RECT pvAttribute,
        int cbAttribute);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    internal static extern int DwmGetWindowAttribute(
        IntPtr hwnd,
        uint dwAttribute,
        out int pvAttribute,
        int cbAttribute);
}
