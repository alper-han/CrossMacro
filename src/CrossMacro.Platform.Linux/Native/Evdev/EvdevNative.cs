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

    public const int O_RDONLY = 0x0000;
    public const int O_NONBLOCK = 0x0800;
}
