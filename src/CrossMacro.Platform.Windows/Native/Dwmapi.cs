
namespace CrossMacro.Platform.Windows.Native;

internal static partial class Dwmapi
{
    internal const uint DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    internal const uint DWMWA_CLOAKED = 14;

    [LibraryImport("dwmapi.dll")]
    internal static partial int DwmGetWindowAttribute(
        IntPtr hwnd,
        uint dwAttribute,
        out RectStruct pvAttribute,
        int cbAttribute);

    [LibraryImport("dwmapi.dll")]
    internal static partial int DwmGetWindowAttribute(
        IntPtr hwnd,
        uint dwAttribute,
        out int pvAttribute,
        int cbAttribute);
}
