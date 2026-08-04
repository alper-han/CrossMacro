
namespace CrossMacro.Core.Services.Playback;

/// <summary>
/// High-precision timing service for playback delays.
/// Uses coarse async waits while reserving the final millisecond for a precise wait.
/// </summary>
public class PlaybackTimingService : IPlaybackTimingService
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
        long deadlineTicks = Stopwatch.GetTimestamp() + MillisecondsToTicks(delayMilliseconds);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (pauseToken.IsPaused)
            {
                var pauseStartTicks = Stopwatch.GetTimestamp();
                Log.Debug("[PlaybackTimingService] Pause detected during delay wait");
                await pauseToken.WaitIfPausedAsync(cancellationToken).ConfigureAwait(false);
                var pausedTicks = Stopwatch.GetTimestamp() - pauseStartTicks;
                deadlineTicks += pausedTicks;
                continue;
            }

            long remainingTicks = deadlineTicks - Stopwatch.GetTimestamp();
            if (remainingTicks <= 0)
            {
                return;
            }

            double remainingMs = TicksToMilliseconds(remainingTicks);
            int coarseDelayMs = Math.Min(
                MaxDelayChunkMs,
                Math.Max(0, Convert.ToInt32(Math.Floor(remainingMs - FinalSpinWindowMs))));
            if (coarseDelayMs > 0)
            {
                await Task.Delay(coarseDelayMs, cancellationToken).ConfigureAwait(false);
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

    private static long MillisecondsToTicks(double milliseconds)
    {
        return Convert.ToInt64(Math.Truncate(milliseconds * Stopwatch.Frequency / 1000d));
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return ticks * 1000d / Stopwatch.Frequency;
    }
}
