
namespace CrossMacro.Infrastructure.Persistence.Macros;

public class PersistedMacroEvent
{
    public EventType Type { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public MacroMouseButton Button { get; init; }
    public long TimestampMicroseconds { get; init; }
    public long DelayMicroseconds { get; init; }
    public bool HasRandomDelay { get; init; }
    public int RandomDelayMinMs { get; init; }
    public int RandomDelayMaxMs { get; init; }
    public int KeyCode { get; init; }
    public MouseCoordinateMode? CoordinateMode { get; init; }
    public MouseCoordinateSpace? CoordinateSpace { get; init; }
    public bool UseCurrentPosition { get; init; }

    public static PersistedMacroEvent FromRuntime(MacroEvent ev) => new()
    {
        Type = ev.Type,
        X = ev.X,
        Y = ev.Y,
        Button = ev.Button,
        TimestampMicroseconds = ev.TimestampMicroseconds,
        DelayMicroseconds = ev.DelayMicroseconds,
        HasRandomDelay = ev.HasRandomDelay,
        RandomDelayMinMs = ev.RandomDelayMinMs,
        RandomDelayMaxMs = ev.RandomDelayMaxMs,
        KeyCode = ev.KeyCode,
        CoordinateMode = ev.CoordinateMode,
        CoordinateSpace = ev.CoordinateSpace,
        UseCurrentPosition = ev.UseCurrentPosition,
    };

    public MacroEvent ToRuntime() => new()
    {
        Type = Type,
        X = X,
        Y = Y,
        Button = Button,
        TimestampMicroseconds = TimestampMicroseconds,
        DelayMicroseconds = DelayMicroseconds,
        HasRandomDelay = HasRandomDelay,
        RandomDelayMinMs = RandomDelayMinMs,
        RandomDelayMaxMs = RandomDelayMaxMs,
        KeyCode = KeyCode,
        CoordinateMode = CoordinateMode,
        CoordinateSpace = CoordinateSpace,
        UseCurrentPosition = UseCurrentPosition,
    };
}
