
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.PortalPipeWire;

internal static partial class PortalPipeWireLibc
{
    [LibraryImport("libc.so.6", SetLastError = true)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int dup(int oldfd);

    [LibraryImport("libc.so.6", SetLastError = true)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int dup(SafeHandle oldfd);

    [LibraryImport("libc.so.6", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int memfd_create(string name, uint flags);

    [LibraryImport("libc.so.6", SetLastError = true)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int ftruncate(int fd, int length);

    [LibraryImport("libc.so.6", SetLastError = true)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial IntPtr mmap(IntPtr addr, UIntPtr length, int prot, int flags, int fd, IntPtr offset);

    [LibraryImport("libc.so.6", SetLastError = true)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int munmap(IntPtr addr, UIntPtr length);

    [LibraryImport("libc.so.6", SetLastError = true)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int close(int fd);
}
