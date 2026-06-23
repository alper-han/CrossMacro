using System.Runtime.InteropServices;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.PortalPipeWire;

internal static partial class PortalPipeWireLibc
{
    [DllImport("libc.so.6", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    public static extern int dup(int oldfd);

    [DllImport("libc.so.6", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    public static extern int memfd_create(string name, uint flags);

    [DllImport("libc.so.6", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    public static extern int ftruncate(int fd, int length);

    [DllImport("libc.so.6", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    public static extern IntPtr mmap(IntPtr addr, UIntPtr length, int prot, int flags, int fd, IntPtr offset);

    [DllImport("libc.so.6", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    public static extern int munmap(IntPtr addr, UIntPtr length);

    [DllImport("libc.so.6", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    public static extern int close(int fd);
}
