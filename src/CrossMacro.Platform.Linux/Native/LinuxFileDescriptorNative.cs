namespace CrossMacro.Platform.Linux.Native;

internal static partial class LinuxFileDescriptorNative
{
    private const short PollIn = 0x001;
    private const short PollOut = 0x004;
    private const short PollError = 0x008;
    private const short PollHangup = 0x010;
    private const short PollInvalid = 0x020;
    private const int ErrnoInterrupted = 4;
    private const int ErrnoWouldBlock = 11;
    private const int FcntlGetFlags = 3;
    private const int FcntlSetFlags = 4;
    private const int OpenNonBlock = 0x800;

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

    [LibraryImport("libc.so.6", SetLastError = true)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial nint write(int fileDescriptor, IntPtr buffer, nuint count);

    [LibraryImport("libc.so.6", SetLastError = true)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int fcntl(int fileDescriptor, int command, int argument);

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

    internal static void SetNonBlocking(int fileDescriptor)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fileDescriptor);

        var flags = fcntl(fileDescriptor, FcntlGetFlags, 0);
        if (flags < 0)
        {
            throw new IOException($"fcntl(F_GETFL) failed errno={Marshal.GetLastPInvokeError().ToString(CultureInfo.InvariantCulture)}.");
        }

        if (fcntl(fileDescriptor, FcntlSetFlags, flags | OpenNonBlock) < 0)
        {
            throw new IOException($"fcntl(F_SETFL) failed errno={Marshal.GetLastPInvokeError().ToString(CultureInfo.InvariantCulture)}.");
        }
    }

    internal static void WriteAll(int fileDescriptor, byte[] data, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fileDescriptor);
        ArgumentNullException.ThrowIfNull(data);

        var dataHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            var buffer = dataHandle.AddrOfPinnedObject();
            var offset = 0;
            while (offset < data.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var bytesWritten = write(
                    fileDescriptor,
                    IntPtr.Add(buffer, offset),
                    (nuint)(data.Length - offset));
                if (bytesWritten > 0)
                {
                    offset += checked((int)bytesWritten);
                    continue;
                }

                if (bytesWritten is 0)
                {
                    throw new IOException("write returned zero before the complete payload was transferred.");
                }

                var error = Marshal.GetLastPInvokeError();
                if (error is ErrnoInterrupted)
                {
                    continue;
                }

                if (error is ErrnoWouldBlock)
                {
                    WaitForWritable(fileDescriptor, cancellationToken);
                    continue;
                }

                throw new IOException($"write failed errno={error.ToString(CultureInfo.InvariantCulture)}.");
            }
        }
        finally
        {
            dataHandle.Free();
        }
    }

    private static void WaitForWritable(int fileDescriptor, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pollFd = new PollFd
            {
                FileDescriptor = fileDescriptor,
                Events = PollOut,
            };
            var result = poll(ref pollFd, count: 1, timeout: 100);
            if (result < 0)
            {
                var error = Marshal.GetLastPInvokeError();
                if (error is ErrnoInterrupted)
                {
                    continue;
                }

                throw new IOException($"poll(POLLOUT) failed errno={error.ToString(CultureInfo.InvariantCulture)}.");
            }

            if (result is 0)
            {
                continue;
            }

            if ((pollFd.Revents & (PollError | PollHangup | PollInvalid)) is not 0)
            {
                throw new IOException("Clipboard data descriptor became unavailable while waiting for writable space.");
            }

            if ((pollFd.Revents & PollOut) is not 0)
            {
                return;
            }
        }
    }

    internal static bool WaitForReadable(int fileDescriptor, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (PollReadable(fileDescriptor, timeoutMilliseconds: 100))
            {
                return true;
            }
        }
    }

    internal static bool PollReadable(int fileDescriptor, int timeoutMilliseconds)
    {
        while (true)
        {
            var pollFd = new PollFd
            {
                FileDescriptor = fileDescriptor,
                Events = PollIn,
            };
            var result = poll(ref pollFd, count: 1, timeout: timeoutMilliseconds);
            if (result < 0 && Marshal.GetLastPInvokeError() == ErrnoInterrupted)
            {
                continue;
            }

            if (result < 0)
            {
                throw new IOException($"poll failed errno={Marshal.GetLastPInvokeError().ToString(CultureInfo.InvariantCulture)}.");
            }

            if (result is 0)
            {
                return false;
            }

            if ((pollFd.Revents & (PollError | PollHangup)) is not 0)
            {
                return true;
            }

            return (pollFd.Revents & PollIn) is not 0;
        }
    }
}
