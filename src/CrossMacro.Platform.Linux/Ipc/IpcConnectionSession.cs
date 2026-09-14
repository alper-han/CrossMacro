namespace CrossMacro.Platform.Linux.Ipc;

/// <summary>
/// Owns one connected transport generation. Detaching this object from the transport
/// transfers all teardown ownership together, so an old reader cannot close a new socket.
/// </summary>
internal sealed class IpcConnectionSession(
    Socket socket,
    NetworkStream stream,
    BinaryReader reader,
    BinaryWriter writer,
    CancellationTokenSource cancellationSource,
    int generation) : IDisposable, IAsyncDisposable
{
    private int _closed;
    public Socket Socket { get; } = socket;
    public NetworkStream Stream { get; } = stream;
    public BinaryReader Reader { get; } = reader;
    public BinaryWriter Writer { get; } = writer;
    public CancellationTokenSource CancellationSource { get; } = cancellationSource;
    public CancellationToken Token { get; } = cancellationSource.Token;
    public int Generation { get; } = generation;
    public Task? ReadTask { get; set; }

    public bool IsConnected
    {
        get
        {
            try
            {
                return Volatile.Read(ref _closed) is 0 && Socket.Connected;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _closed, 1) is not 0)
        {
            return;
        }
        try
        {
            CancellationSource.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // A completed reader may already have released cancellation resources.
        }
        finally
        {
            CloseIo();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _closed, 1) is not 0)
        {
            return;
        }
        try
        {
            await CancellationSource.CancelAsync().ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            // A completed reader may already have released cancellation resources.
        }
        finally
        {
            CloseIo();
        }
    }

    private void CloseIo()
    {
        DisposeSafely(Reader);
        DisposeSafely(Writer);
        DisposeSafely(Stream);
        DisposeSafely(Socket);
        if (ReadTask is null || ReadTask.IsCompleted)
        {
            DisposeSafely(CancellationSource);
        }
        else
        {
            // A read failure can initiate its own teardown; never await that reader here.
            _ = ReleaseCancellationAfterReaderAsync(ReadTask);
        }
    }

    private async Task ReleaseCancellationAfterReaderAsync(Task reader)
    {
        try
        {
            await reader.ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The transport reports read failures through its callback boundary.
        }
        finally
        {
            DisposeSafely(CancellationSource);
        }
    }

    private static void DisposeSafely(IDisposable value)
    {
        try
        {
            value.Dispose();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Complete teardown even when a failed stream cannot flush during disposal.
        }
    }
}
