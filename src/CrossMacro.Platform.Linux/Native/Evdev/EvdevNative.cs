using System;
using System.Runtime.InteropServices;

namespace CrossMacro.Platform.Linux.Native.Evdev;

public static class EvdevNative
{
    private const string LibC = "libc";

    public const ulong EVIOCGNAME_256 = 0x81004506; 
    
    public const ulong EVIOCGBIT_EV = 0x80044520;
    public const ulong EVIOCGBIT_KEY = 0x80044521;
    public const ulong EVIOCGBIT_REL = 0x80044522;
    public const ulong EVIOCGBIT_ABS = 0x80044523;
    public const ulong EVIOCGPROP = 0x80044509;

    // Get device ID (vendor, product, version)
    // _IOC(_IOC_READ, 'E', 0x02, sizeof(input_id)) = 8 bytes
    public const ulong EVIOCGID = 0x80084502;

    // Get current key/button state (for SYN_DROPPED resync)
    // _IOC(_IOC_READ, 'E', 0x18, KEY_MAX/8+1) where KEY_MAX=0x2FF
    // Result: 96 bytes bitmap of currently pressed keys
    public const ulong EVIOCGKEY = 0x80604518;    

    [DllImport(LibC, SetLastError = true)]
    public static extern int open(string pathname, int flags);

    [DllImport(LibC, SetLastError = true)]
    public static extern int close(int fd);

    [DllImport(LibC, SetLastError = true)]
    public static extern IntPtr read(int fd, IntPtr buf, IntPtr count);

    [DllImport(LibC, SetLastError = true)]
    public static extern int ioctl(int fd, ulong request, byte[] data);
    
    [DllImport(LibC, SetLastError = true)]
    public static extern int ioctl(int fd, ulong request, IntPtr data);

    // Flags
    public const int O_RDONLY = 0x0000;
    public const int O_NONBLOCK = 0x0800;
}
