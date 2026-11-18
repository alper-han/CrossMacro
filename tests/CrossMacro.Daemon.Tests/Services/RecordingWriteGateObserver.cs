namespace CrossMacro.Daemon.Tests.Services;

/// <summary>Lets a test wait for the gate to reach a queue length, and run code when a writer is woken.</summary>
internal sealed class RecordingWriteGateObserver : IOrderedWriteGateObserver
{
    private readonly ConcurrentDictionary<int, TaskCompletionSource> _queueLengths = new();

    public Action? OnWriterGranted { get; set; }

    /// <summary>Completes once a writer has joined a queue holding <paramref name="queueLength"/> writers.</summary>
    public Task WhenQueuedAsync(int queueLength) => QueueLength(queueLength).Task;

    public void WriterQueued(int queueLength) => _ = QueueLength(queueLength).TrySetResult();

    public void WriterGranted() => OnWriterGranted?.Invoke();

    private TaskCompletionSource QueueLength(int queueLength) =>
        _queueLengths.GetOrAdd(queueLength, static _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
}
