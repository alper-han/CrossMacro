
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class WaylandCaptureCancellation(ScreenReadOptions options)
{
    private readonly CancellationToken _cancellationToken = options.CancellationToken;
    private readonly long _deadlineTimestamp = GetDeadlineTimestamp(options.Timeout);

    private static long GetDeadlineTimestamp(TimeSpan? timeout)
    {
        if (timeout is not { } timeoutValue)
        {
            return long.MaxValue;
        }

        var now = Stopwatch.GetTimestamp();
        var timeoutTicks = timeoutValue.TotalSeconds * Stopwatch.Frequency;
        return timeoutTicks >= long.MaxValue - now
            ? long.MaxValue
            : now + (long)timeoutTicks;
    }

    public void ThrowIfCancellationRequested()
    {
        _cancellationToken.ThrowIfCancellationRequested();
        if (Stopwatch.GetTimestamp() >= _deadlineTimestamp)
        {
            throw new TimeoutException("Wayland screen capture timed out.");
        }
    }

    public int GetPollTimeoutMilliseconds()
    {
        ThrowIfCancellationRequested();
        if (_deadlineTimestamp == long.MaxValue)
        {
            return 100;
        }

        var remainingTicks = _deadlineTimestamp - Stopwatch.GetTimestamp();
        if (remainingTicks <= 0)
        {
            throw new TimeoutException("Wayland screen capture timed out.");
        }

        var milliseconds = (long)Math.Ceiling(remainingTicks * 1000d / Stopwatch.Frequency);
        return (int)Math.Clamp(milliseconds, 1, 100);
    }
}
