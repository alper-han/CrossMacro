
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

    [StructLayout(LayoutKind.Sequential)]
    internal struct PollFd
    {
        public int Fd;
        public short Events;
        public short Revents;
    }

    [LibraryImport(LibC, SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    public static partial int open(string pathname, int flags);

    [LibraryImport(LibC, SetLastError = true)]
    public static partial int close(int fd);

    [LibraryImport(LibC, SetLastError = true)]
    internal static partial nint read(int fd, [Out] byte[] buf, nint count);

    [LibraryImport(LibC, SetLastError = true)]
    internal static partial int poll(Span<PollFd> fds, nuint count, int timeout);

    [LibraryImport(LibC, SetLastError = true)]
    internal static partial int eventfd(uint initval, int flags);

    [LibraryImport(LibC, SetLastError = true)]
    internal static partial nint write(int fd, ref ulong buf, nint count);

    [LibraryImport(LibC, SetLastError = true)]
    public static partial int ioctl(int fd, ulong request, [Out] byte[] data);

    [LibraryImport(LibC, SetLastError = true)]
    public static partial int ioctl(int fd, ulong request, IntPtr data);

    public const int O_RDONLY = 0x0000;
    public const int O_NONBLOCK = 0x0800;

    internal const short POLLIN = 0x001;
    internal const int EFD_CLOEXEC = 0x80000;
    internal const int EFD_NONBLOCK = 0x800;
}
