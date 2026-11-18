namespace CrossMacro.Infrastructure.Services.Scripting;

/// <summary>
/// Places compiled events on the macro timeline. A <c>delay</c> before the first event is the sequence's
/// initial delay; later delays are pending until the next event takes them, and whatever is still pending
/// at the end is the trailing delay.
/// </summary>
internal sealed class RunScriptEventTimeline
{
    private long _timestampMicroseconds;

    public MacroSequence Sequence { get; } = new()
    {
        Name = "Run Script",
        IsAbsoluteCoordinates = false,
        SkipInitialZeroZero = true,
    };

    public Delay Initial { get; private set; }

    /// <summary>The delay waiting for the next event; after the last event, the sequence's trailing delay.</summary>
    public Delay Trailing { get; private set; }

    public bool HasEvents { get; private set; }

    /// <summary>Delay accumulated from one or more <c>delay</c> steps.</summary>
    public readonly record struct Delay(long FixedMicroseconds, bool HasRandom, int RandomMinMs, int RandomMaxMs)
    {
        public Delay Plus(long fixedMicroseconds, bool hasRandom, int randomMinMs, int randomMaxMs) => new(
            FixedMicroseconds + fixedMicroseconds,
            HasRandom || hasRandom,
            hasRandom ? RandomMinMs + randomMinMs : RandomMinMs,
            hasRandom ? RandomMaxMs + randomMaxMs : RandomMaxMs);
    }

    public void AddDelay(long fixedMicroseconds, bool hasRandom, int randomMinMs, int randomMaxMs)
    {
        if (HasEvents)
        {
            Trailing = Trailing.Plus(fixedMicroseconds, hasRandom, randomMinMs, randomMaxMs);
        }
        else
        {
            Initial = Initial.Plus(fixedMicroseconds, hasRandom, randomMinMs, randomMaxMs);
        }
    }

    public void Emit(MacroEvent macroEvent)
    {
        var pending = Trailing;
        macroEvent.DelayMicroseconds = checked(macroEvent.DelayMicroseconds + pending.FixedMicroseconds);
        macroEvent.HasRandomDelay = pending.HasRandom;
        macroEvent.RandomDelayMinMs = pending.RandomMinMs;
        macroEvent.RandomDelayMaxMs = pending.RandomMaxMs;
        _timestampMicroseconds += macroEvent.DelayMicroseconds;
        if (pending.HasRandom)
        {
            _timestampMicroseconds += (long)pending.RandomMinMs * MacroTiming.MicrosecondsPerMillisecond;
        }

        macroEvent.TimestampMicroseconds = _timestampMicroseconds;
        Trailing = default;
        Sequence.Events.Add(macroEvent);
        HasEvents = true;
    }
}
