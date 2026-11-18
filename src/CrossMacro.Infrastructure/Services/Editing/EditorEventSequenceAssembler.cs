namespace CrossMacro.Infrastructure.Services.Editing;

/// <summary>
/// Assembles projected events into a <see cref="MacroSequence"/>: delay placeholders (events of type
/// <see cref="EventType.None"/>) are folded into the next real event, and trailing delay is kept for looped macros.
/// </summary>
internal sealed class EditorEventSequenceAssembler(MacroSequence sequence)
{
    private long _timestampMicroseconds;
    private long _pendingDelayMicroseconds;
    private bool _hasPendingRandomDelay;
    private int _pendingRandomDelayMinMs;
    private int _pendingRandomDelayMaxMs;

    internal void Append(EditorAction action, IEnumerable<MacroEvent> events)
    {
        var actionStartEventIndex = sequence.Events.Count;
        var actionEventCount = 0;
        foreach (var ev in events)
        {
            if (ev.Type is EventType.None)
            {
                AccumulateDelay(ev);
                continue;
            }

            sequence.Events.Add(WithPendingDelay(ev));
            actionEventCount++;
        }

        if (action.Type is EditorActionType.TextInput && actionEventCount > 0)
        {
            sequence.TextInputBoundaries.Add(new TextInputBoundary(actionStartEventIndex, actionEventCount, action.Text));
        }

        if (!string.IsNullOrEmpty(action.Comment))
        {
            sequence.ActionComments.Add(new ActionCommentBoundary(actionStartEventIndex, actionEventCount, 0, 0, action.Comment));
        }
    }

    /// <summary>Stores any delay that no event consumed as the sequence's trailing delay.</summary>
    internal void ApplyTrailingDelay()
    {
        if (_pendingDelayMicroseconds > 0 || _hasPendingRandomDelay)
        {
            sequence.TrailingDelayMicroseconds = _pendingDelayMicroseconds;
            sequence.HasTrailingRandomDelay = _hasPendingRandomDelay;
            sequence.TrailingDelayMinMs = _pendingRandomDelayMinMs;
            sequence.TrailingDelayMaxMs = _pendingRandomDelayMaxMs;
        }
    }

    private void AccumulateDelay(MacroEvent placeholder)
    {
        _pendingDelayMicroseconds += placeholder.DelayMicroseconds;
        if (placeholder.HasRandomDelay)
        {
            _hasPendingRandomDelay = true;
            _pendingRandomDelayMinMs += placeholder.RandomDelayMinMs;
            _pendingRandomDelayMaxMs += placeholder.RandomDelayMaxMs;
        }
    }

    private MacroEvent WithPendingDelay(MacroEvent ev)
    {
        var eventToAdd = ev;
        eventToAdd.DelayMicroseconds += _pendingDelayMicroseconds;
        if (_hasPendingRandomDelay)
        {
            eventToAdd.HasRandomDelay = true;
            eventToAdd.RandomDelayMinMs += _pendingRandomDelayMinMs;
            eventToAdd.RandomDelayMaxMs += _pendingRandomDelayMaxMs;
        }

        eventToAdd.TimestampMicroseconds = _timestampMicroseconds;
        _timestampMicroseconds += eventToAdd.DelayMicroseconds;
        if (eventToAdd.HasRandomDelay)
        {
            _timestampMicroseconds += (long)eventToAdd.RandomDelayMinMs * MacroTiming.MicrosecondsPerMillisecond;
        }

        _pendingDelayMicroseconds = 0;
        _hasPendingRandomDelay = false;
        _pendingRandomDelayMinMs = 0;
        _pendingRandomDelayMaxMs = 0;
        return eventToAdd;
    }
}
