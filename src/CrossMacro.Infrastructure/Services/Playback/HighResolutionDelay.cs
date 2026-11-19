namespace CrossMacro.Infrastructure.Services.Playback;

/// <summary>
/// Cancellation-aware high-resolution delay: coarse <see cref="Task.Delay(TimeSpan, TimeProvider, CancellationToken)"/>
/// chunks followed by a short spin for the final millisecond. When a pause token is supplied, time spent paused
/// does not count toward the delay.
/// </summary>
internal static class HighResolutionDelay
{
    private const double FinalSpinWindowMilliseconds = 1d;
    private const int MaximumCoarseDelayMilliseconds = 50;

    public static Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        WaitAsync(delay.TotalMilliseconds, pauseToken: null, cancellationToken);

    public static async Task WaitAsync(
        double delayMilliseconds,
        IPlaybackPauseToken? pauseToken,
        CancellationToken cancellationToken)
    {
        if (!double.IsFinite(delayMilliseconds))
        {
            throw new ArgumentOutOfRangeException(
                nameof(delayMilliseconds),
                delayMilliseconds,
                "Playback delay must be finite.");
        }

        if (delayMilliseconds <= 0)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var deadlineTicks = Stopwatch.GetTimestamp() + MillisecondsToTicks(delayMilliseconds);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (pauseToken is { IsPaused: true })
            {
                var pauseStartTicks = Stopwatch.GetTimestamp();
                await pauseToken.WaitIfPausedAsync(cancellationToken).ConfigureAwait(false);
                deadlineTicks += Stopwatch.GetTimestamp() - pauseStartTicks;
                continue;
            }

            var remainingTicks = deadlineTicks - Stopwatch.GetTimestamp();
            if (remainingTicks <= 0)
            {
                return;
            }

            var remainingMilliseconds = remainingTicks * 1_000d / Stopwatch.Frequency;
            var coarseDelayMilliseconds = Math.Min(
                MaximumCoarseDelayMilliseconds,
                Math.Floor(remainingMilliseconds - FinalSpinWindowMilliseconds));
            if (coarseDelayMilliseconds >= 1d)
            {
                await Task.Delay(
                    TimeSpan.FromMilliseconds(coarseDelayMilliseconds),
                    TimeProvider.System,
                    cancellationToken).ConfigureAwait(false);
                continue;
            }

            SpinUntilDeadline(deadlineTicks, pauseToken, cancellationToken);
        }
    }

    private static void SpinUntilDeadline(
        long deadlineTicks,
        IPlaybackPauseToken? pauseToken,
        CancellationToken cancellationToken)
    {
        var spinner = new SpinWait();
        while (deadlineTicks - Stopwatch.GetTimestamp() > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (pauseToken is { IsPaused: true })
            {
                return;
            }

            spinner.SpinOnce(sleep1Threshold: -1);
        }
    }

    private static long MillisecondsToTicks(double milliseconds) =>
        checked((long)Math.Ceiling(milliseconds * Stopwatch.Frequency / 1_000d));
}
