namespace CrossMacro.Infrastructure.Services.Playback;

/// <summary>Provides cancellation-aware high-resolution delays.</summary>
internal static class HighResolutionDelay
{
    private const double FinalSpinWindowMilliseconds = 1d;
    private const int MaximumCoarseDelayMilliseconds = 50;

    public static async Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        if (delay <= TimeSpan.Zero)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var deadlineTicks = Stopwatch.GetTimestamp() + ToStopwatchTicks(delay);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var remainingTicks = deadlineTicks - Stopwatch.GetTimestamp();
            if (remainingTicks <= 0)
            {
                return;
            }

            var remainingMilliseconds = remainingTicks * 1_000d / Stopwatch.Frequency;
            var coarseDelayMilliseconds = Math.Min(
                MaximumCoarseDelayMilliseconds,
                Math.Max(0d, Math.Floor(remainingMilliseconds - FinalSpinWindowMilliseconds)));
            if (coarseDelayMilliseconds >= 1d)
            {
                await Task.Delay(
                    TimeSpan.FromMilliseconds(coarseDelayMilliseconds),
                    TimeProvider.System,
                    cancellationToken).ConfigureAwait(false);
                continue;
            }

            var spinner = new SpinWait();
            while (Stopwatch.GetTimestamp() < deadlineTicks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                spinner.SpinOnce(sleep1Threshold: -1);
            }

            return;
        }
    }

    private static long ToStopwatchTicks(TimeSpan delay) =>
        checked((long)Math.Ceiling(delay.TotalSeconds * Stopwatch.Frequency));
}
