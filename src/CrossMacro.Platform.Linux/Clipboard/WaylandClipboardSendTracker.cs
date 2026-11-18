namespace CrossMacro.Platform.Linux.Clipboard;

/// <summary>
/// Runs the clipboard data transfers a Wayland compositor requests through the source's
/// <c>send</c> event. Each transfer owns the file descriptor it was given, is bounded by a
/// deadline, is cancelled on disposal, and is awaited so none outlives its connection.
/// </summary>
internal sealed class WaylandClipboardSendTracker(TimeSpan deadline) : IDisposable
{
    private static readonly TimeSpan DisposeWait = TimeSpan.FromSeconds(2);

    private readonly TimeSpan _deadline = deadline;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Lock _gate = new();
    private readonly Dictionary<int, Task> _active = [];
    private int _nextId;
    private bool _disposed;

    /// <summary>
    /// Starts writing <paramref name="data"/> to <paramref name="fileDescriptor"/> and takes ownership of
    /// the descriptor. When the tracker is already disposed the descriptor is closed and nothing is sent.
    /// </summary>
    public bool TryStart(int fileDescriptor, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        int id;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationTokenSource transferCancellation;
        lock (_gate)
        {
            if (_disposed)
            {
                LinuxFileDescriptorNative.Close(fileDescriptor);
                return false;
            }

            id = _nextId++;
            _active.Add(id, completion.Task);
            transferCancellation = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
        }

        transferCancellation.CancelAfter(_deadline);
        _ = Task.Run(
            () => Transfer(id, fileDescriptor, data, transferCancellation, completion),
            CancellationToken.None);
        return true;
    }

    public void Dispose()
    {
        Task[] pending;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            pending = [.. _active.Values];
        }

        _shutdown.Cancel();
        try
        {
            _ = Task.WaitAll(pending, DisposeWait);
        }
        catch (AggregateException ex)
        {
            Log.Debug(ex, "[WaylandNativeClipboard] A clipboard transfer failed while the connection was closing");
        }

        _shutdown.Dispose();
    }

    private void Transfer(
        int id,
        int fileDescriptor,
        byte[] data,
        CancellationTokenSource transferCancellation,
        TaskCompletionSource completion)
    {
        try
        {
            using var handle = new SafeFileHandle((IntPtr)fileDescriptor, ownsHandle: true);

            // The compositor hands over a blocking pipe; a blocked write(2) cannot be cancelled,
            // so switch to non-blocking and let WriteAll poll under the deadline.
            LinuxFileDescriptorNative.SetNonBlocking(fileDescriptor);
            LinuxFileDescriptorNative.WriteAll(fileDescriptor, data, transferCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            Log.Debug("[WaylandNativeClipboard] Clipboard transfer was cancelled before the reader consumed all data");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Debug(ex, "[WaylandNativeClipboard] Failed to send clipboard data");
        }
        finally
        {
            transferCancellation.Dispose();
            lock (_gate)
            {
                _ = _active.Remove(id);
            }

            _ = completion.TrySetResult();
        }
    }
}
