using System.Runtime.InteropServices;

namespace CrossMacro.Platform.Windows.Native;

[ComImport]
[Guid("a5cd92ff-29be-454c-8d04-d82879fb3f1b")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IVirtualDesktopManager
{
    [PreserveSig]
    int IsWindowOnCurrentVirtualDesktop(
        IntPtr topLevelWindow,
        [MarshalAs(UnmanagedType.Bool)] out bool onCurrentDesktop);

    [PreserveSig]
    int GetWindowDesktopId(
        IntPtr topLevelWindow,
        out Guid desktopId);

    [PreserveSig]
    int MoveWindowToDesktop(
        IntPtr topLevelWindow,
        ref Guid desktopId);
}

[ComImport]
[Guid("aa509086-5ca9-4c25-8f95-589d3c07b48a")]
internal sealed class VirtualDesktopManager
{
}
