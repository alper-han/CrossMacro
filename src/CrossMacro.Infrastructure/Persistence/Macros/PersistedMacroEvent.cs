
namespace CrossMacro.Infrastructure.Persistence.Macros;

public class PersistedMacroEvent
{
    public EventType Type { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public MouseButton Button { get; init; }
    public long Timestamp { get; init; }
    public int DelayMs { get; init; }
    public bool HasRandomDelay { get; init; }
    public int RandomDelayMinMs { get; init; }
    public int RandomDelayMaxMs { get; init; }
    public int KeyCode { get; init; }
    public MouseCoordinateMode? CoordinateMode { get; init; }
    public bool UseCurrentPosition { get; init; }

    public static PersistedMacroEvent FromRuntime(MacroEvent ev) => new()
    {
        Type = ev.Type,
        X = ev.X,
        Y = ev.Y,
        Button = ev.Button,
        Timestamp = ev.Timestamp,
        DelayMs = ev.DelayMs,
        HasRandomDelay = ev.HasRandomDelay,
        RandomDelayMinMs = ev.RandomDelayMinMs,
        RandomDelayMaxMs = ev.RandomDelayMaxMs,
        KeyCode = ev.KeyCode,
        CoordinateMode = ev.CoordinateMode,
        UseCurrentPosition = ev.UseCurrentPosition,
    };

    public MacroEvent ToRuntime() => new()
    {
        Type = Type,
        X = X,
        Y = Y,
        Button = Button,
        Timestamp = Timestamp,
        DelayMs = DelayMs,
        HasRandomDelay = HasRandomDelay,
        RandomDelayMinMs = RandomDelayMinMs,
        RandomDelayMaxMs = RandomDelayMaxMs,
        KeyCode = KeyCode,
        CoordinateMode = CoordinateMode,
        UseCurrentPosition = UseCurrentPosition,
    };
}
