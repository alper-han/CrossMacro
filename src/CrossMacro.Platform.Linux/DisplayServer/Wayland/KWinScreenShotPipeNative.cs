namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal static partial class KWinScreenShotPipeNative
{
    internal const int F_GETFL = 3;
    internal const int F_SETFL = 4;
    internal const int O_NONBLOCK = 0x800;
    internal const int O_CLOEXEC = 0x80000;

    [LibraryImport("libc.so.6", EntryPoint = "pipe2", SetLastError = true)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int pipe2([Out] int[] fileDescriptors, int flags);

    [LibraryImport("libc.so.6", SetLastError = true)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int fcntl(int fileDescriptor, int command, int argument);

    [LibraryImport("libc.so.6", SetLastError = true)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int close(int fileDescriptor);
}
