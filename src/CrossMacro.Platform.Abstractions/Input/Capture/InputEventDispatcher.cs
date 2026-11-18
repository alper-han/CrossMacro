using System.Collections.Concurrent;

namespace CrossMacro.Platform.Abstractions.Input.Capture;

/// <summary>
/// Hands captured events from a latency-critical native callback (low-level hook, event tap)
/// to a dedicated thread that runs subscribers. The queue is bounded: when it is full
/// <see cref="TryEnqueue"/> returns <see langword="false"/> immediately instead of blocking the callback.
/// </summary>
public sealed class InputEventDispatcher : IDisposable
{
    public const int DefaultCapacity = 4096;

    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(5);

    private readonly BlockingCollection<CapturedInputEvent> _queue;
    private readonly Action<CapturedInputEvent> _dispatch;
    private readonly Action<Exception> _reportError;
    private readonly Thread _thread;
    private int _accepting = 1;
    private int _completed;

    /// <param name="threadName">Name of the dispatch thread.</param>
    /// <param name="dispatch">Runs on the dispatch thread for every queued event.</param>
    /// <param name="reportError">Runs on the dispatch thread when <paramref name="dispatch"/> throws; it must not throw.</param>
    /// <param name="capacity">Maximum number of queued events.</param>
    public InputEventDispatcher(
        string threadName,
        Action<CapturedInputEvent> dispatch,
        Action<Exception> reportError,
        int capacity = DefaultCapacity)
    {
        ArgumentException.ThrowIfNullOrEmpty(threadName);
        ArgumentNullException.ThrowIfNull(dispatch);
        ArgumentNullException.ThrowIfNull(reportError);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        _dispatch = dispatch;
        _reportError = reportError;
        _queue = new BlockingCollection<CapturedInputEvent>(
            new ConcurrentQueue<CapturedInputEvent>(),
            capacity);
        _thread = new Thread(DispatchLoop)
        {
            IsBackground = true,
            Name = threadName,
        };
        _thread.Start();
    }

    /// <summary>Whether the dispatch thread has drained the queue and exited.</summary>
    public bool IsCompleted => Volatile.Read(ref _completed) is not 0;

    public bool TryEnqueue(CapturedInputEvent inputEvent)
    {
        if (Volatile.Read(ref _accepting) is 0)
        {
            return false;
        }

        try
        {
            return _queue.TryAdd(inputEvent);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>
    /// Stops accepting events, lets the dispatch thread drain what is queued and waits up to five seconds for it.
    /// Check <see cref="IsCompleted"/> afterwards when a still-running subscriber matters.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _accepting, 0) is not 0)
        {
            try
            {
                _queue.CompleteAdding();
            }
            catch (ObjectDisposedException)
            {
                // The dispatch thread already drained and released the queue.
            }
        }

        if (!ReferenceEquals(Thread.CurrentThread, _thread) && _thread.IsAlive)
        {
            _ = _thread.Join(ShutdownTimeout);
        }
    }

    private void DispatchLoop()
    {
        try
        {
            foreach (CapturedInputEvent inputEvent in _queue.GetConsumingEnumerable(CancellationToken.None))
            {
                try
                {
                    _dispatch(inputEvent);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    _reportError(ex);
                }
            }
        }
        finally
        {
            Volatile.Write(ref _completed, 1);
            _queue.Dispose();
        }
    }
}
