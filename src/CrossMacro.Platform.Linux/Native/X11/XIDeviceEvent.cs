using System;
using System.Runtime.InteropServices;

namespace CrossMacro.Platform.Linux.Native.X11;

[StructLayout(LayoutKind.Sequential, Size = 192)]
public struct XIDeviceEvent
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
    public IntPtr root;
    public IntPtr event_window;
    public IntPtr child;
    public double root_x;
    public double root_y;
    public double event_x;
    public double event_y;
    public int flags;
    public XIValuatorState buttons;
    public XIValuatorState valuators;
    public XIModifierState mods;
    public XIModifierState group;
}
