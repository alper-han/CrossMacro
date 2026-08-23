namespace CrossMacro.Platform.MacOS.Services;

internal sealed class MacOSInputEventDispatcher : IDisposable
{
    internal const int DefaultCapacity = 4096;

    private readonly BlockingCollection<CapturedInputEventArgs> _queue;
    private readonly Action<CapturedInputEventArgs> _dispatch;
    private readonly Action<Exception> _reportError;
    private readonly Thread _thread;
    private int _accepting = 1;
    private int _completed;

    public MacOSInputEventDispatcher(
        Action<CapturedInputEventArgs> dispatch,
        Action<Exception> reportError,
        int capacity = DefaultCapacity)
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        ArgumentNullException.ThrowIfNull(reportError);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        _dispatch = dispatch;
        _reportError = reportError;
        _queue = new BlockingCollection<CapturedInputEventArgs>(
            new ConcurrentQueue<CapturedInputEventArgs>(),
            capacity);
        _thread = new Thread(DispatchLoop)
        {
            IsBackground = true,
            Name = "MacOSInputDispatch",
        };
        _thread.Start();
    }

    public bool IsCompleted => Volatile.Read(ref _completed) is not 0;

    public bool TryEnqueue(CapturedInputEventArgs inputEvent)
    {
        ArgumentNullException.ThrowIfNull(inputEvent);
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
            _thread.Join();
        }

        GC.SuppressFinalize(this);
    }

    private void DispatchLoop()
    {
        try
        {
            foreach (CapturedInputEventArgs inputEvent in _queue.GetConsumingEnumerable(CancellationToken.None))
            {
                try
                {
                    _dispatch(inputEvent);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    ReportError(ex);
                }
            }
        }
        finally
        {
            Volatile.Write(ref _completed, 1);
            _queue.Dispose();
        }
    }

    private void ReportError(Exception exception)
    {
        try
        {
            _reportError(exception);
        }
        catch (Exception errorHandlerException) when (errorHandlerException is not OutOfMemoryException)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[MacOSInputCapture] Error handler threw: {errorHandlerException}");
        }
    }
}
