namespace CrossMacro.Infrastructure.Services.Playback;

/// <summary>
/// Runtime-owned high precision playback timing implementation.
/// </summary>
/// <remarks>
/// The timing contract remains in Core, while Stopwatch/Task.Delay/SpinWait
/// orchestration belongs to the outer runtime. The legacy Core concrete type
/// remains available for source compatibility and isolated Core tests.
/// </remarks>
internal sealed class SystemPlaybackTimingService : IPlaybackTimingService
{
    private const int MaxDelayChunkMs = 50;
    private const double FinalSpinWindowMs = 1.0;
    private const int YieldSpinInterval = 8;

    public async Task WaitAsync(
        double delayMilliseconds,
        IPlaybackPauseToken pauseToken,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pauseToken);

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

            if (pauseToken.IsPaused)
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

            var remainingMilliseconds = TicksToMilliseconds(remainingTicks);
            var coarseDelayMilliseconds = Math.Min(
                MaxDelayChunkMs,
                Math.Max(0, Convert.ToInt32(Math.Floor(remainingMilliseconds - FinalSpinWindowMs))));
            if (coarseDelayMilliseconds > 0)
            {
                await Task.Delay(coarseDelayMilliseconds, cancellationToken).ConfigureAwait(false);
                continue;
            }

            SpinUntilDeadline(deadlineTicks, pauseToken, cancellationToken);
        }
    }

    private static void SpinUntilDeadline(
        long deadlineTicks,
        IPlaybackPauseToken pauseToken,
        CancellationToken cancellationToken)
    {
        var spinner = new SpinWait();
        while (deadlineTicks - Stopwatch.GetTimestamp() > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (pauseToken.IsPaused)
            {
                break;
            }

            spinner.SpinOnce(sleep1Threshold: -1);
            if (spinner.Count % YieldSpinInterval is 0)
            {
                _ = Thread.Yield();
            }
        }
    }

    private static long MillisecondsToTicks(double milliseconds) =>
        Convert.ToInt64(Math.Truncate(milliseconds * Stopwatch.Frequency / 1000d));

    private static double TicksToMilliseconds(long ticks) =>
        ticks * 1000d / Stopwatch.Frequency;
}
