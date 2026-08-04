
namespace CrossMacro.Core.Models;

/// <summary>
/// Represents a single input event in a macro sequence.
/// </summary>
public struct MacroEvent : IEquatable<MacroEvent>
{
    /// <summary>
    /// Type of the event
    /// </summary>
    public EventType Type { get; set; }

    /// <summary>
    /// X coordinate or horizontal delta for coordinate-bearing mouse events.
    /// The value is interpreted by the effective coordinate mode resolved from
    /// <see cref="CoordinateMode" /> or the macro's legacy coordinate metadata.
    /// </summary>
    public int X { get; set; }

    /// <summary>
    /// Y coordinate or vertical delta for coordinate-bearing mouse events.
    /// The value is interpreted by the effective coordinate mode resolved from
    /// <see cref="CoordinateMode" /> or the macro's legacy coordinate metadata.
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    /// Mouse button for button press, button release, click, and scroll events.
    /// </summary>
    public MacroMouseButton Button { get; set; }

    /// <summary>
    /// Timestamp when the event was recorded (milliseconds since recording start)
    /// </summary>
    public long Timestamp { get; set; }

    /// <summary>
    /// Delay until next event (milliseconds)
    /// </summary>
    public int DelayMs { get; set; }

    /// <summary>
    /// Whether the delay includes a randomized component.
    /// </summary>
    public bool HasRandomDelay { get; set; }

    /// <summary>
    /// Minimum randomized delay in milliseconds.
    /// </summary>
    public int RandomDelayMinMs { get; set; }

    /// <summary>
    /// Maximum randomized delay in milliseconds.
    /// </summary>
    public int RandomDelayMaxMs { get; set; }

    /// <summary>
    /// Keyboard key code for key press and key release events.
    /// Uses Linux input key codes (e.g., 30 = KEY_A, 57 = KEY_SPACE)
    /// </summary>
    public int KeyCode { get; set; }

    /// <summary>
    /// Optional event-level coordinate mode for coordinate-bearing mouse events.
    /// When unset, the macro-wide legacy coordinate metadata is used as the fallback.
    /// Ignored for keyboard events, scroll events, and current-position mouse button events.
    /// </summary>
    public MouseCoordinateMode? CoordinateMode { get; set; }

    /// <summary>
    /// Coordinate unit space for this event. When absent, legacy relative events
    /// retain raw-device behavior and absolute events use logical desktop pixels.
    /// </summary>
    public MouseCoordinateSpace? CoordinateSpace { get; set; }

    /// <summary>
    /// Whether a non-scroll mouse button event should use the live cursor
    /// position at playback time instead of the stored coordinates.
    /// </summary>
    public bool UseCurrentPosition { get; set; }

    public readonly bool Equals(MacroEvent other)
    {
        return Type == other.Type
            && X == other.X
            && Y == other.Y
            && Button == other.Button
            && Timestamp == other.Timestamp
            && DelayMs == other.DelayMs
            && HasRandomDelay == other.HasRandomDelay
            && RandomDelayMinMs == other.RandomDelayMinMs
            && RandomDelayMaxMs == other.RandomDelayMaxMs
            && KeyCode == other.KeyCode
            && CoordinateMode == other.CoordinateMode
            && CoordinateSpace == other.CoordinateSpace
            && UseCurrentPosition == other.UseCurrentPosition;
    }

    public override readonly bool Equals([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] object? obj) => obj is MacroEvent other && Equals(other);

    public override readonly int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Type);
        hash.Add(X);
        hash.Add(Y);
        hash.Add(Button);
        hash.Add(Timestamp);
        hash.Add(DelayMs);
        hash.Add(HasRandomDelay);
        hash.Add(RandomDelayMinMs);
        hash.Add(RandomDelayMaxMs);
        hash.Add(KeyCode);
        hash.Add(CoordinateMode);
        hash.Add(CoordinateSpace);
        hash.Add(UseCurrentPosition);
        return hash.ToHashCode();
    }

    public static bool operator ==(MacroEvent left, MacroEvent right) => left.Equals(right);

    public static bool operator !=(MacroEvent left, MacroEvent right) => !left.Equals(right);
}
