
namespace CrossMacro.Platform.Linux.Native.Evdev;

public static partial class EvdevNative
{
    private const string LibC = "libc";

    public const ulong EVIOCGNAME_256 = 0x81004506;
    public const ulong EVIOCGBIT_EV = 0x80044520;
    public const ulong EVIOCGBIT_KEY = 0x80044521;
    public const ulong EVIOCGBIT_REL = 0x80044522;
    public const ulong EVIOCGBIT_ABS = 0x80044523;
    public const ulong EVIOCGPROP = 0x80044509;
    public const ulong EVIOCGID = 0x80084502;
    public const ulong EVIOCGKEY = 0x80604518;

    public static ulong EVIOCGBIT(int eventType, int length)
    {
        const ulong iocRead = 2;
        const int iocDirShift = 30;
        const int iocSizeShift = 16;
        const int iocTypeShift = 8;
        const int evdevIoctlType = 0x45;
        const int evdevGetBitBase = 0x20;

        return (iocRead << iocDirShift) |
               ((ulong)length << iocSizeShift) |
               ((ulong)evdevIoctlType << iocTypeShift) |
               (uint)(evdevGetBitBase + eventType);
    }

    [LibraryImport(LibC, SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    public static partial int open(string pathname, int flags);

    [LibraryImport(LibC, SetLastError = true)]
    public static partial int close(int fd);

    [LibraryImport(LibC, SetLastError = true)]
    public static partial IntPtr read(int fd, IntPtr buf, IntPtr count);

    [LibraryImport(LibC, SetLastError = true)]
    public static partial int ioctl(int fd, ulong request, byte[] data);

    [LibraryImport(LibC, SetLastError = true)]
    public static partial int ioctl(int fd, ulong request, IntPtr data);

    public const int O_RDONLY = 0x0000;
    public const int O_NONBLOCK = 0x0800;
}
