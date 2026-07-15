using System;
using System.Runtime.InteropServices;

namespace CrossMacro.Platform.Linux.Native.X11
{
    // ReSharper disable InconsistentNaming
    // ReSharper disable IdentifierTypo

    [StructLayout(LayoutKind.Sequential)]
    public struct XIEventMask
    {
        public int DeviceId;
        public int MaskLen;
        public IntPtr Mask;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct XEvent
    {
        [FieldOffset(0)] public int type;
        [FieldOffset(0)] public XGenericEventCookie xcookie;
    }

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

    [StructLayout(LayoutKind.Sequential)]
    public struct XIValuatorState
    {
        public int mask_len;
        public IntPtr mask;
    }

    [StructLayout(LayoutKind.Sequential)]
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

    [StructLayout(LayoutKind.Sequential)]
    public struct XIModifierState
    {
        public int @base;
        public int latched;
        public int locked;
        public int effective;
    }

    public static class XInput2Consts
    {

        public const int GenericEvent = 35;
        public const int XIAllMasterDevices = 1;

        public const int XI_Motion = 6;
        public const int XI_RawKeyPress = 13;
        public const int XI_RawKeyRelease = 14;
        public const int XI_RawButtonPress = 15;
        public const int XI_RawButtonRelease = 16;
        public const int XI_RawMotion = 17;

        // XInput2 version constants
        public const int XINPUT2_MAJOR_VERSION = 2;
        public const int XINPUT2_MINOR_VERSION = 2;

        // XEvent structure size for memory allocation
        public const int XEVENT_STRUCT_SIZE = 192;

        // X11 scroll button constants
        public const int X11_SCROLL_UP = 4;
        public const int X11_SCROLL_DOWN = 5;
        public const int X11_SCROLL_LEFT = 6;
        public const int X11_SCROLL_RIGHT = 7;

        // Scroll delta value (standard scroll unit)
        public const int SCROLL_DELTA = 120;

        // Scroll axis identifiers
        public const int SCROLL_AXIS_VERTICAL = 0;
        public const int SCROLL_AXIS_HORIZONTAL = 1;

        public static void SetMask(byte[] mask, int eventType)
        {
            mask[eventType >> 3] |= (byte)(1 << (eventType & 7));
        }

        public static bool IsBitSet(byte[] mask, int bit)
        {
            return (mask[bit >> 3] & (1 << (bit & 7))) is not 0;
        }

        public static bool IsBitSet(IntPtr maskPtr, int maskLen, int bit)
        {
            int byteIndex = bit >> 3;
            if (byteIndex >= maskLen) return false;
            byte b = Marshal.ReadByte(maskPtr, byteIndex);
            return (b & (1 << (bit & 7))) is not 0;
        }
    }
}
