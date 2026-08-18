namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class KWinScreenShotPipe : IDisposable, IAsyncDisposable
{
    private readonly KWinScreenShotPipeStream _readStream;
    private int _disposed;

    public KWinScreenShotPipe()
    {
        var fileDescriptors = new int[2];
        if (KWinScreenShotPipeNative.pipe2(fileDescriptors, KWinScreenShotPipeNative.O_CLOEXEC) is not 0)
        {
            throw new InvalidOperationException($"pipe2(KWin ScreenShot2) failed errno={Marshal.GetLastPInvokeError().ToString(CultureInfo.InvariantCulture)}.");
        }

        try
        {
            _readStream = new KWinScreenShotPipeStream(fileDescriptors[0]);
            WriteHandle = new SafeFileHandle(new IntPtr(fileDescriptors[1]), ownsHandle: true);
        }
        catch
        {
            _ = KWinScreenShotPipeNative.close(fileDescriptors[0]);
            _ = KWinScreenShotPipeNative.close(fileDescriptors[1]);
            throw;
        }
    }

    public Stream ReadStream => _readStream;

    public SafeFileHandle WriteHandle { get; }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) is not 0)
        {
            return;
        }

        _readStream.Dispose();
        WriteHandle.Dispose();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) is not 0)
        {
            return;
        }

        await _readStream.DisposeAsync().ConfigureAwait(false);
        WriteHandle.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed class KWinScreenShotPipeStream : System.IO.Pipes.PipeStream
    {
        public KWinScreenShotPipeStream(int fileDescriptor)
            : base(System.IO.Pipes.PipeDirection.In, 0)
        {
            var handle = new SafePipeHandle(new IntPtr(fileDescriptor), ownsHandle: true);
            try
            {
                var flags = KWinScreenShotPipeNative.fcntl(fileDescriptor, KWinScreenShotPipeNative.F_GETFL, 0);
                if (flags is -1 || KWinScreenShotPipeNative.fcntl(fileDescriptor, KWinScreenShotPipeNative.F_SETFL, flags | KWinScreenShotPipeNative.O_NONBLOCK) is -1)
                {
                    throw new InvalidOperationException($"fcntl(KWin ScreenShot2 pipe) failed errno={Marshal.GetLastPInvokeError().ToString(CultureInfo.InvariantCulture)}.");
                }

                InitializeHandle(handle, isExposed: false, isAsync: true);
                IsConnected = true;
            }
            catch
            {
                handle.Dispose();
                throw;
            }
        }
    }
}
