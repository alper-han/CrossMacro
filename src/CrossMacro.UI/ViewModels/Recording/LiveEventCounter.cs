namespace CrossMacro.UI.ViewModels.Recording;

/// <summary>
/// Collects per-event counts from the recorder thread and delivers them to the UI thread in batches, so a burst
/// of input never queues one UI update per event. Counts belong to a session; stale sessions are ignored.
/// </summary>
internal sealed class LiveEventCounter(IUiDispatcher dispatcher, Func<bool> isDisposed, Action<long, long, long> applyDelta)
{
    private LiveCounterUpdateState? _activeState;
    private long _nextSessionId;

    private sealed class LiveCounterUpdateState(long sessionId)
    {
        public long SessionId { get; } = sessionId;

        public long PendingEventCount;
        public long PendingMouseEventCount;
        public long PendingKeyboardEventCount;
        public int IsDrainScheduled;
    }

    internal void Activate()
    {
        var sessionId = Interlocked.Increment(ref _nextSessionId);
        Volatile.Write(ref _activeState, new LiveCounterUpdateState(sessionId: sessionId));
    }

    internal void Deactivate()
    {
        var state = Volatile.Read(ref _activeState);
        if (state is null)
        {
            return;
        }

        _ = Interlocked.CompareExchange(ref _activeState, value: null, comparand: state);
    }

    internal void OnEventRecorded(MacroEvent macroEvent)
    {
        var state = Volatile.Read(ref _activeState);
        if (state is null || !IsActive(state))
        {
            return;
        }

        AddDelta(state, macroEvent);
        ScheduleDrain(state);
    }

    private bool IsActive(LiveCounterUpdateState state)
    {
        return !isDisposed() &&
            state.SessionId != 0 &&
            ReferenceEquals(state, Volatile.Read(ref _activeState));
    }

    private static void AddDelta(LiveCounterUpdateState state, MacroEvent macroEvent)
    {
        SaturatingIncrement(ref state.PendingEventCount);

        switch (macroEvent.Type)
        {
            case EventType.ButtonPress:
            case EventType.ButtonRelease:
            case EventType.MouseMove:
            case EventType.Click:
                SaturatingIncrement(ref state.PendingMouseEventCount);
                break;
            case EventType.KeyPress:
            case EventType.KeyRelease:
                SaturatingIncrement(ref state.PendingKeyboardEventCount);
                break;
        }
    }

    private void ScheduleDrain(LiveCounterUpdateState state)
    {
        if (!IsActive(state) ||
            Interlocked.CompareExchange(ref state.IsDrainScheduled, value: 1, comparand: 0) is not 0)
        {
            return;
        }

        try
        {
            dispatcher.Post(() => Drain(state));
        }
        catch
        {
            Volatile.Write(ref state.IsDrainScheduled, 0);
            throw;
        }
    }

    private void Drain(LiveCounterUpdateState state)
    {
        if (!IsActive(state))
        {
            return;
        }

        var eventCount = Interlocked.Exchange(ref state.PendingEventCount, 0);
        var mouseEventCount = Interlocked.Exchange(ref state.PendingMouseEventCount, 0);
        var keyboardEventCount = Interlocked.Exchange(ref state.PendingKeyboardEventCount, 0);

        if (!IsActive(state))
        {
            return;
        }

        try
        {
            applyDelta(eventCount, mouseEventCount, keyboardEventCount);
        }
        finally
        {
            Volatile.Write(ref state.IsDrainScheduled, 0);

            if (Volatile.Read(ref state.PendingEventCount) is not 0 ||
                Volatile.Read(ref state.PendingMouseEventCount) is not 0 ||
                Volatile.Read(ref state.PendingKeyboardEventCount) is not 0)
            {
                ScheduleDrain(state);
            }
        }
    }

    private static void SaturatingIncrement(ref long value)
    {
        while (true)
        {
            var currentValue = Volatile.Read(ref value);
            if (currentValue == long.MaxValue ||
                Interlocked.CompareExchange(ref value, currentValue + 1, currentValue) == currentValue)
            {
                return;
            }
        }
    }
}
