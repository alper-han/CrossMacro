using System;
using System.Runtime.InteropServices;

namespace CrossMacro.Platform.Linux.Native.X11;

[StructLayout(LayoutKind.Sequential)]
public struct XGenericEventCookie
{
    public int type;
    public IntPtr serial;
    [MarshalAs(UnmanagedType.Bool)] public bool send_event;
    public IntPtr display;
    public int extension;
    public int evtype;
    public int cookie;
    public IntPtr data;
}
