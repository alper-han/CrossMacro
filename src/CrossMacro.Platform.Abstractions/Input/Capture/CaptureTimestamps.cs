using System.Diagnostics;

namespace CrossMacro.Platform.Abstractions.Input.Capture;

/// <summary>Timestamps attached to <see cref="CapturedInputEvent"/>s by capture backends.</summary>
public static class CaptureTimestamps
{
    public static long UnixMilliseconds() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public static long MonotonicMicroseconds() => ToMicroseconds(Stopwatch.GetTimestamp(), Stopwatch.Frequency);

    /// <summary>Converts stopwatch ticks to microseconds without losing precision for large tick counts.</summary>
    public static long ToMicroseconds(long timestamp, long frequency)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(frequency, 0);
        return checked(
            (timestamp / frequency * 1_000_000L)
            + (timestamp % frequency * 1_000_000L / frequency));
    }
}
