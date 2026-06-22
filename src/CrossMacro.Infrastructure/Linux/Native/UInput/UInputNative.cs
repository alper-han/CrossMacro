using System;
using System.Runtime.InteropServices;

namespace CrossMacro.Infrastructure.Linux.Native.UInput;

public static class UInputNative
{
    private const string LibC = "libc";

    public const int O_WRONLY = 1;
    public const int O_NONBLOCK = 2048;

    public const uint UI_SET_EVBIT = 0x40045564;
    public const uint UI_SET_KEYBIT = 0x40045565;
    public const uint UI_SET_RELBIT = 0x40045566;
    public const uint UI_SET_ABSBIT = 0x40045567;
    public const uint UI_DEV_CREATE = 0x5501;
    public const uint UI_DEV_DESTROY = 0x5502;
    public const uint UI_SET_PROPBIT = 0x4004556e;

    public const ushort EV_SYN = 0x00;
    public const ushort EV_KEY = 0x01;
    public const ushort EV_REL = 0x02;
    public const ushort EV_ABS = 0x03;

    public const ushort REL_X = 0x00;
    public const ushort REL_Y = 0x01;
    public const ushort REL_HWHEEL = 0x06;
    public const ushort REL_WHEEL = 0x08;

    public const ushort ABS_X = 0x00;
    public const ushort ABS_Y = 0x01;

    public const ushort BTN_LEFT = 0x110;
    public const ushort BTN_RIGHT = 0x111;
    public const ushort BTN_MIDDLE = 0x112;
    public const ushort BTN_SIDE = 0x113;
    public const ushort BTN_EXTRA = 0x114;
    public const ushort BTN_FORWARD = 0x115;
    public const ushort BTN_BACK = 0x116;
    public const ushort BTN_TASK = 0x117;

    public const ushort BTN_TOUCH = 0x14a;
    public const ushort BTN_TOOL_FINGER = 0x145;

    public const ushort BTN_MISC = 0x100;
    public const ushort BTN_0 = 0x100;
    public const ushort BTN_1 = 0x101;
    public const ushort BTN_2 = 0x102;
    public const ushort BTN_3 = 0x103;
    public const ushort BTN_4 = 0x104;
    public const ushort BTN_5 = 0x105;
    public const ushort BTN_6 = 0x106;
    public const ushort BTN_7 = 0x107;
    public const ushort BTN_8 = 0x108;
    public const ushort BTN_9 = 0x109;

    public const ushort BTN_JOYSTICK = 0x120;
    public const ushort BTN_TRIGGER = 0x120;
    public const ushort BTN_THUMB = 0x121;
    public const ushort BTN_THUMB2 = 0x122;
    public const ushort BTN_TOP = 0x123;
    public const ushort BTN_TOP2 = 0x124;
    public const ushort BTN_PINKIE = 0x125;
    public const ushort BTN_BASE = 0x126;
    public const ushort BTN_BASE2 = 0x127;
    public const ushort BTN_BASE3 = 0x128;
    public const ushort BTN_BASE4 = 0x129;
    public const ushort BTN_BASE5 = 0x12a;
    public const ushort BTN_BASE6 = 0x12b;
    public const ushort BTN_DEAD = 0x12f;
    public const ushort BTN_GAMEPAD = 0x130;
    public const ushort BTN_SOUTH = 0x130;
    public const ushort BTN_A = 0x130;
    public const ushort BTN_EAST = 0x131;
    public const ushort BTN_B = 0x131;
    public const ushort BTN_C = 0x132;
    public const ushort BTN_NORTH = 0x133;
    public const ushort BTN_X = 0x133;
    public const ushort BTN_WEST = 0x134;
    public const ushort BTN_Y = 0x134;
    public const ushort BTN_Z = 0x135;
    public const ushort BTN_TL = 0x136;
    public const ushort BTN_TR = 0x137;
    public const ushort BTN_TL2 = 0x138;
    public const ushort BTN_TR2 = 0x139;
    public const ushort BTN_SELECT = 0x13a;
    public const ushort BTN_START = 0x13b;
    public const ushort BTN_MODE = 0x13c;
    public const ushort BTN_THUMBL = 0x13d;
    public const ushort BTN_THUMBR = 0x13e;
    
    public const ushort ABS_MT_SLOT = 0x2f;
    public const ushort ABS_MT_POSITION_X = 0x35;
    public const ushort ABS_MT_POSITION_Y = 0x36;

    public const ushort SYN_REPORT = 0;
    public const ushort SYN_DROPPED = 3;

    public const ushort INPUT_PROP_POINTER = 0x00;
    public const ushort INPUT_PROP_DIRECT = 0x01;

    public const ushort BUS_USB = 0x03;
    public const ushort BUS_VIRTUAL = 0x06;

    [StructLayout(LayoutKind.Sequential)]
    public struct input_event
    {
        public IntPtr time_sec;
        public IntPtr time_usec;
        public ushort type;
        public ushort code;
        public int value;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct uinput_user_dev
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string name;
        public ushort id_bustype;
        public ushort id_vendor;
        public ushort id_product;
        public ushort id_version;
        public int ff_effects_max;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public int[] absmax;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public int[] absmin;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public int[] absfuzz;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public int[] absflat;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct uinput_setup
    {
        public input_id id;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string name;
        public uint ff_effects_max;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct input_id
    {
        public ushort bustype;
        public ushort vendor;
        public ushort product;
        public ushort version;
    }

    [DllImport(LibC, SetLastError = true)]
    public static extern int open([MarshalAs(UnmanagedType.LPStr)] string pathname, int flags);

    [DllImport(LibC, SetLastError = true)]
    public static extern int close(int fd);

    [DllImport(LibC, SetLastError = true, EntryPoint = "write")]
    public static extern IntPtr write_setup(int fd, ref uinput_user_dev buf, IntPtr count);

    [DllImport(LibC, SetLastError = true)]
    public static extern IntPtr write(int fd, ref input_event buf, IntPtr count);

    [DllImport(LibC, SetLastError = true)]
    public static extern IntPtr write(int fd, IntPtr buf, IntPtr count);

    [DllImport(LibC, SetLastError = true)]
    public static extern int ioctl(int fd, uint request, int value);

    [DllImport(LibC, SetLastError = true)]
    public static extern int ioctl(int fd, uint request, ref uinput_user_dev value);

    public static bool IsMouseButton(ushort code)
    {
        return code >= BTN_LEFT && code <= BTN_TASK;
    }
    
    public static bool IsGamepadButton(ushort code)
    {
        return code >= BTN_JOYSTICK && code <= BTN_THUMBR;
    }
}
