using System;
using System.Runtime.InteropServices;

namespace CrossMacro.Platform.Linux.Native.X11;

[StructLayout(LayoutKind.Sequential)]
public struct XIRawEvent
{
    public int type;
    public IntPtr serial;
    [MarshalAs(UnmanagedType.Bool)] public bool send_event;
    public IntPtr display;
    public int extension;
    public int evtype;
    public IntPtr time;
    public int deviceid;
    public int sourceid;
    public int detail;
    public int flags;
    public XIValuatorState valuators;
    public IntPtr raw_values;
}
