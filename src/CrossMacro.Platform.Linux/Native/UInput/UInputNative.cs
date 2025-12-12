using System;
using System.Runtime.InteropServices;

namespace CrossMacro.Platform.Linux.Native.UInput;

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
    public const ushort REL_WHEEL = 0x08;
    
    public const ushort ABS_X = 0x00;
    public const ushort ABS_Y = 0x01;
    
    public const ushort BTN_LEFT = 0x110;
    public const ushort BTN_RIGHT = 0x111;
    public const ushort BTN_MIDDLE = 0x112;
    
    public const ushort BTN_TOUCH = 0x14a;
    public const ushort BTN_TOOL_FINGER = 0x145;
    
    public const ushort ABS_MT_SLOT = 0x2f;
    public const ushort ABS_MT_POSITION_X = 0x35;
    public const ushort ABS_MT_POSITION_Y = 0x36;
    
    public const ushort SYN_REPORT = 0;
    
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
}
