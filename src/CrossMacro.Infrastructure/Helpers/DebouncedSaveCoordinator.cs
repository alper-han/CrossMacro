using System.Runtime.ExceptionServices;

namespace CrossMacro.Infrastructure.Helpers;

internal sealed class DebouncedSaveCoordinator : IDisposable
{
    private readonly Lock _gate = new();
    private readonly Func<Task> _saveAsync;
    private readonly Timer _timer;
    private readonly TimeSpan _delay;

    private TaskCompletionSource? _pendingCompletion;
    private Task _activeSave = Task.CompletedTask;
    private bool _savePending;
    private bool _saveRunning;
    private bool _disposed;

    public DebouncedSaveCoordinator(Func<Task> saveAsync, TimeSpan delay)
    {
        _saveAsync = saveAsync ?? throw new ArgumentNullException(nameof(saveAsync));
        _delay = delay > TimeSpan.Zero
            ? delay
            : throw new ArgumentOutOfRangeException(nameof(delay));
        _timer = new Timer(OnTimerElapsed, state: null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public Task RequestAsync()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            _savePending = true;
            _pendingCompletion ??= CreateCompletionSource();
            _ = _timer.Change(_delay, Timeout.InfiniteTimeSpan);
            return _pendingCompletion.Task;
        }
    }

    public async Task<bool> FlushAsync(CancellationToken cancellationToken = default)
    {
        var flushed = false;

        while (true)
        {
            Task activeSave;

            lock (_gate)
            {
                if (_savePending)
                {
                    flushed = true;
                    _ = _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                    StartSave_NoLock();
                }

                flushed |= _saveRunning;
                activeSave = _activeSave;
                if (!_savePending && !_saveRunning)
                {
                    return flushed;
                }
            }

            await activeSave.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _ = _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        Exception? failure = null;

        try
        {
            while (true)
            {
                Task activeSave;

                lock (_gate)
                {
                    if (_savePending && !_saveRunning)
                    {
                        StartSave_NoLock();
                    }

                    activeSave = _activeSave;
                    if (!_savePending && !_saveRunning)
                    {
                        break;
                    }
                }

                try
                {
                    activeSave.GetAwaiter().GetResult();
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    failure ??= ex;
                }
            }
        }
        finally
        {
            _timer.Dispose();
        }

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private void OnTimerElapsed(object? state)
    {
        lock (_gate)
        {
            if (_disposed || !_savePending)
            {
                return;
            }

            StartSave_NoLock();
        }
    }

    private void StartSave_NoLock()
    {
        if (!_savePending || _pendingCompletion is null || _saveRunning)
        {
            return;
        }

        _savePending = false;
        _ = _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        var completion = _pendingCompletion;
        _pendingCompletion = null;
        _saveRunning = true;
        _activeSave = Task.Run(() => ExecuteSaveAsync(completion), CancellationToken.None);
        _ = _activeSave.ContinueWith(
            static completedTask => _ = completedTask.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task ExecuteSaveAsync(TaskCompletionSource completion)
    {
        try
        {
            await _saveAsync().ConfigureAwait(false);
            _ = completion.TrySetResult();
        }
        catch (Exception ex)
        {
            _ = completion.TrySetException(ex);
            throw;
        }
        finally
        {
            lock (_gate)
            {
                _saveRunning = false;
                if (_savePending)
                {
                    if (_disposed)
                    {
                        StartSave_NoLock();
                    }
                    else
                    {
                        _ = _timer.Change(_delay, Timeout.InfiniteTimeSpan);
                    }
                }
            }
        }
    }

    private static TaskCompletionSource CreateCompletionSource() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
