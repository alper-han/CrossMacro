namespace CrossMacro.Platform.Abstractions;

/// <summary>
/// Carries one input event reported by an input capture provider.
/// </summary>
public sealed class CapturedInputEventArgs : EventArgs
{
    public InputEventType Type { get; init; }

    public int Code { get; init; }

    public int Value { get; init; }

    public long Timestamp { get; init; }

    /// <summary>Optional monotonic capture timestamp in microseconds.</summary>
    public long TimestampMicroseconds { get; init; }

    public string? DeviceName { get; init; }

    public CapturedInputEvent Event => new()
    {
        Type = Type,
        Code = Code,
        Value = Value,
        Timestamp = Timestamp,
        TimestampMicroseconds = TimestampMicroseconds,
        DeviceName = DeviceName,
    };

    public CapturedInputEventArgs()
    {
    }

    public CapturedInputEventArgs(CapturedInputEvent inputEvent)
    {
        Type = inputEvent.Type;
        Code = inputEvent.Code;
        Value = inputEvent.Value;
        Timestamp = inputEvent.Timestamp;
        TimestampMicroseconds = inputEvent.TimestampMicroseconds;
        DeviceName = inputEvent.DeviceName;
    }
}
