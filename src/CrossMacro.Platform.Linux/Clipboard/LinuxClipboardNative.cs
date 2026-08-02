namespace CrossMacro.Platform.Linux.Clipboard;

internal static partial class LinuxClipboardNative
{
    private const short PollIn = 0x001;
    private const short PollError = 0x008;
    private const short PollHangup = 0x010;
    private const int ErrnoInterrupted = 4;

    [StructLayout(LayoutKind.Sequential)]
    private struct PollFd
    {
        public int FileDescriptor;
        public short Events;
        public short Revents;
    }

    [LibraryImport("libc.so.6", SetLastError = true)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int poll(ref PollFd fds, nint count, int timeout);

    [LibraryImport("libc.so.6", SetLastError = true)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int pipe2([Out] int[] pipefd, int flags);

    [LibraryImport("libc.so.6", SetLastError = true)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int close(int fileDescriptor);

    internal static (int Read, int Write) CreatePipe()
    {
        var descriptors = new int[2];
        if (pipe2(descriptors, 0) is not 0)
        {
            throw new IOException($"pipe2 failed errno={Marshal.GetLastPInvokeError().ToString(CultureInfo.InvariantCulture)}.");
        }

        return (descriptors[0], descriptors[1]);
    }

    internal static void Close(int fileDescriptor)
    {
        if (fileDescriptor >= 0)
        {
            _ = close(fileDescriptor);
        }
    }

    internal static bool WaitForReadable(int fileDescriptor, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pollFd = new PollFd
            {
                FileDescriptor = fileDescriptor,
                Events = PollIn,
            };
            var result = poll(ref pollFd, new nint(1), 100);
            if (result < 0)
            {
                if (Marshal.GetLastPInvokeError() == ErrnoInterrupted)
                {
                    continue;
                }

                throw new IOException($"poll failed errno={Marshal.GetLastPInvokeError().ToString(CultureInfo.InvariantCulture)}.");
            }

            if (result is 0)
            {
                continue;
            }

            if ((pollFd.Revents & (PollError | PollHangup)) is not 0)
            {
                return true;
            }

            return (pollFd.Revents & PollIn) is not 0;
        }
    }
}
