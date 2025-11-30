using System;
using System.Runtime.InteropServices;

namespace CrossMacro.Native.Evdev;

public static class EvdevNative
{
    private const string LibC = "libc";

    // ioctl commands
    // _IOR('E', 0x06, char[NAME_MAX]) - get device name
    public const ulong EVIOCGNAME_256 = 0x81004506; 
    
    // _IOR('E', 0x20, int[...]) - get bitfield
    public const ulong EVIOCGBIT_EV = 0x80044520; // EV capabilities
    public const ulong EVIOCGBIT_KEY = 0x80044521; // KEY capabilities
    public const ulong EVIOCGBIT_REL = 0x80044522; // REL capabilities

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
