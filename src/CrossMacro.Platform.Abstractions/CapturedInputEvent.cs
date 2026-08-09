namespace CrossMacro.Platform.Abstractions;

public readonly struct CapturedInputEvent : IEquatable<CapturedInputEvent>
{
    public InputEventType Type { get; init; }

    public int Code { get; init; }

    public int Value { get; init; }

    public long Timestamp { get; init; }

    /// <summary>Optional monotonic timestamp supplied by the capture backend.</summary>
    public long TimestampMicroseconds { get; init; }

    public string? DeviceName { get; init; }

    public bool Equals(CapturedInputEvent other) =>
        Type == other.Type
        && Code == other.Code
        && Value == other.Value
        && Timestamp == other.Timestamp
        && TimestampMicroseconds == other.TimestampMicroseconds
        && string.Equals(DeviceName, other.DeviceName, StringComparison.Ordinal);

    public override bool Equals([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] object? obj) => obj is CapturedInputEvent other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Type, Code, Value, Timestamp, TimestampMicroseconds, DeviceName);

    public static bool operator ==(CapturedInputEvent left, CapturedInputEvent right) => left.Equals(right);

    public static bool operator !=(CapturedInputEvent left, CapturedInputEvent right) => !left.Equals(right);
}
