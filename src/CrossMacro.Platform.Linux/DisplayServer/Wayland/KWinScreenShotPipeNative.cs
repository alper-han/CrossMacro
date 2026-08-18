namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal static class KWinScreenShotPipeNative
{
    internal const int F_GETFL = 3;
    internal const int F_SETFL = 4;
    internal const int O_NONBLOCK = 0x800;
    internal const int O_CLOEXEC = 0x80000;

    [DllImport("libc.so.6", EntryPoint = "pipe2", SetLastError = true)]
    internal static extern int pipe2([Out] int[] fileDescriptors, int flags);

    [DllImport("libc.so.6", SetLastError = true)]
    internal static extern int fcntl(int fileDescriptor, int command, int argument);

    [DllImport("libc.so.6", SetLastError = true)]
    internal static extern int close(int fileDescriptor);
}
