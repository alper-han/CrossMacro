
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class WaylandCaptureCancellation(ScreenReadOptions options)
{
    private readonly CancellationToken _cancellationToken = options.CancellationToken;
    private readonly long _deadlineTimestamp = options.Timeout is { } timeout
            ? Stopwatch.GetTimestamp() + (long)(timeout.TotalSeconds * Stopwatch.Frequency)
            : long.MaxValue;

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
