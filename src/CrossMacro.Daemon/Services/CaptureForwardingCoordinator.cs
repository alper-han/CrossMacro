
namespace CrossMacro.Daemon.Services;

internal sealed class CaptureForwardingCoordinator(int maxBufferedCaptureEvents) : IAsyncDisposable
{
    private readonly int _maxBufferedCaptureEvents = maxBufferedCaptureEvents;
    private readonly Lock _sync = new();
    private readonly CaptureForwardingState _state = new();
    private readonly Queue<ForwardedEvent> _events = new();
    private TaskCompletionSource? _eventSignal;
    private readonly CancellationTokenSource _shutdownCts = new();
    private Task? _forwardingTask;
    private Task? _disposeTask;
    private long _nextSequence;
    private long _processedSequence;
    private TaskCompletionSource? _drainCompletion;
    private long _drainTarget;
    private bool _disposed;

    public int BeginPendingGeneration()
    {
        lock (_sync)
        {
            var generation = ++_state.NextGeneration;
            _state.PendingGeneration = generation;
            ResetPendingBuffer(_state);
            return generation;
        }
    }

    public Action<UInputNative.input_event> CreateEventForwarder(int generation, DaemonProtocolSession session)
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return static _ => { };
            }

            _forwardingTask ??= ForwardEventsAsync();
        }

        return inputEvent => Enqueue(inputEvent, generation, session);
    }

    public CaptureActivation ActivateGeneration(int requestGeneration)
    {
        Queue<UInputNative.input_event>? bufferedEvents = null;
        int droppedPendingCaptureEvents;

        lock (_sync)
        {
            droppedPendingCaptureEvents = _state.DroppedPendingCaptureEvents;

            if (_state.BufferedCaptureEvents.Count > 0)
            {
                bufferedEvents = new Queue<UInputNative.input_event>(_state.BufferedCaptureEvents);
                _state.BufferedCaptureEvents.Clear();
            }

            _state.ActiveGeneration = requestGeneration;
            _state.PendingGeneration = 0;
            _state.CaptureForwardingEnabled = true;
            _state.DroppedPendingCaptureEvents = 0;
        }

        return new CaptureActivation(droppedPendingCaptureEvents, bufferedEvents);
    }

    public void ResetAfterFailedStart(int requestGeneration)
    {
        lock (_sync)
        {
            if (_state.PendingGeneration == requestGeneration)
            {
                _state.PendingGeneration = 0;
            }

            _state.ActiveGeneration = 0;
            _state.CaptureForwardingEnabled = false;
            ResetPendingBuffer(_state);
        }
    }

    public void Stop()
    {
        lock (_sync)
        {
            _state.PendingGeneration = 0;
            _state.ActiveGeneration = 0;
            _state.CaptureForwardingEnabled = false;
            ResetPendingBuffer(_state);
            _events.Clear();
            _processedSequence = _nextSequence;
            CompleteDrainLocked();
        }
    }

    public Task DrainAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (_processedSequence >= _nextSequence)
            {
                return Task.CompletedTask;
            }

            _drainTarget = _nextSequence;
            return (_drainCompletion ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously))
                .Task
                .WaitAsync(cancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_sync)
        {
            _disposed = true;
            _events.Clear();
            _processedSequence = _nextSequence;
            CompleteDrainLocked();
            if (_eventSignal is { Task.IsCompleted: false } signal)
            {
                signal.SetResult();
            }
            _disposeTask ??= DisposeCoreAsync();
            return new ValueTask(_disposeTask);
        }
    }

    private async Task DisposeCoreAsync()
    {
        Task? forwardingTask;
        lock (_sync)
        {
            forwardingTask = _forwardingTask;
        }

        await _shutdownCts.CancelAsync().ConfigureAwait(false);

        if (forwardingTask is not null)
        {
            await forwardingTask.ConfigureAwait(false);
        }

        _shutdownCts.Dispose();
    }

    private void Enqueue(UInputNative.input_event inputEvent, int generation, DaemonProtocolSession session)
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            if (_events.Count >= _maxBufferedCaptureEvents)
            {
                var droppedEvent = _events.Dequeue();
                _processedSequence = Math.Max(_processedSequence, droppedEvent.Sequence);
                if (droppedEvent.Generation == _state.PendingGeneration)
                {
                    _state.DroppedPendingCaptureEvents++;
                }
            }

            _events.Enqueue(new ForwardedEvent(inputEvent, generation, session, ++_nextSequence));
            _eventSignal ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!_eventSignal.Task.IsCompleted)
            {
                _eventSignal.SetResult();
            }
        }
    }

    private async Task ForwardEventsAsync()
    {
        try
        {
            while (true)
            {
                ForwardedEvent? forwardedEvent = null;
                Task? signalTask = null;
                lock (_sync)
                {
                    if (_events.Count is 0)
                    {
                        if (_disposed)
                        {
                            return;
                        }

                        _eventSignal ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                        signalTask = _eventSignal.Task;
                    }
                    else
                    {
                        forwardedEvent = _events.Dequeue();
                    }
                }

                if (signalTask is not null)
                {
                    await signalTask.WaitAsync(_shutdownCts.Token).ConfigureAwait(false);
                    lock (_sync)
                    {
                        _eventSignal = null;
                    }
                    continue;
                }

                if (forwardedEvent is not { } eventToForward)
                {
                    continue;
                }

                await ForwardEventAsync(eventToForward).ConfigureAwait(false);

                lock (_sync)
                {
                    _processedSequence = Math.Max(_processedSequence, eventToForward.Sequence);
                    CompleteDrainLocked();
                }
            }
        }
        catch (OperationCanceledException) when (_shutdownCts.IsCancellationRequested)
        {
            return;
        }
    }

    private void CompleteDrainLocked()
    {
        if (_drainCompletion is null || _processedSequence < _drainTarget)
        {
            return;
        }

        _drainCompletion.SetResult();
        _drainCompletion = null;
    }

    private async Task ForwardEventAsync(ForwardedEvent forwardedEvent)
    {
        var inputEvent = forwardedEvent.InputEvent;
        var generation = forwardedEvent.Generation;
        var session = forwardedEvent.Session;
        if (session.Disconnected)
        {
            return;
        }

        try
        {
            var shouldWriteEvent = false;

            lock (_sync)
            {
                if (generation == _state.PendingGeneration)
                {
                    if (_state.BufferedCaptureEvents.Count >= _maxBufferedCaptureEvents)
                    {
                        _ = _state.BufferedCaptureEvents.Dequeue();
                        _state.DroppedPendingCaptureEvents++;
                    }

                    _state.BufferedCaptureEvents.Enqueue(inputEvent);
                    return;
                }

                if (_state.CaptureForwardingEnabled && generation == _state.ActiveGeneration)
                {
                    shouldWriteEvent = true;
                }
            }

            if (!shouldWriteEvent)
            {
                return;
            }

            var isSynReport = inputEvent.type == Platform.Linux.Native.UInput.UInputNative.EV_SYN &&
                              inputEvent.code == Platform.Linux.Native.UInput.UInputNative.SYN_REPORT;

            using (await session.WriterGate.EnterAsync(_shutdownCts.Token).ConfigureAwait(false))
            {
                if (session.Disconnected)
                {
                    return;
                }

                lock (_sync)
                {
                    if (!_state.CaptureForwardingEnabled || generation != _state.ActiveGeneration)
                    {
                        return;
                    }
                }

                session.WriteInputEvent(inputEvent);

                if (isSynReport)
                {
                    await session.Stream.FlushAsync(_shutdownCts.Token).ConfigureAwait(false);
                }
            }
        }
        catch (IOException)
        {
            session.MarkDisconnected();
            Log.Debug("[SessionHandler] Stream closed, stopping event forwarding");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            session.MarkDisconnected();
            Log.Debug(ex, "[SessionHandler] Failed to write input event");
        }
    }

    private readonly record struct ForwardedEvent(
        UInputNative.input_event InputEvent,
        int Generation,
        DaemonProtocolSession Session,
        long Sequence);

    private static void ResetPendingBuffer(CaptureForwardingState captureState)
    {
        captureState.BufferedCaptureEvents.Clear();
        captureState.DroppedPendingCaptureEvents = 0;
    }

    internal readonly record struct CaptureActivation(
        int DroppedPendingCaptureEvents,
        Queue<UInputNative.input_event>? BufferedEvents)
    {
        public bool HasBufferedEvents => BufferedEvents is { Count: > 0 };
    }

    private sealed class CaptureForwardingState
    {
        public int NextGeneration { get; set; }
        public int PendingGeneration { get; set; }
        public int ActiveGeneration { get; set; }
        public bool CaptureForwardingEnabled { get; set; }
        public int DroppedPendingCaptureEvents { get; set; }
        public Queue<UInputNative.input_event> BufferedCaptureEvents { get; } = new();
    }
}
