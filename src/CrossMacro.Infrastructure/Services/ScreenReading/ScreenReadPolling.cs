namespace CrossMacro.Infrastructure.Services.ScreenReading;

internal static class ScreenReadPolling
{
    public static DateTimeOffset GetDeadline(TimeSpan timeout) =>
        TimeProvider.System.GetUtcNow() + timeout;

    public static TimeSpan GetRemaining(DateTimeOffset deadline)
    {
        var remaining = deadline - TimeProvider.System.GetUtcNow();
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    public static bool HasExpired(DateTimeOffset deadline) =>
        TimeProvider.System.GetUtcNow() >= deadline;

    public static TimeSpan GetDelay(DateTimeOffset deadline, TimeSpan pollInterval)
    {
        var remaining = GetRemaining(deadline);
        return remaining < pollInterval ? remaining : pollInterval;
    }
}
