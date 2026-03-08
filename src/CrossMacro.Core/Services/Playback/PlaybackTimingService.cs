using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Logging;

namespace CrossMacro.Core.Services.Playback;

/// <summary>
/// High-precision timing service for playback delays.
/// Uses coarse async waits with a very short final spin window.
/// </summary>
public class PlaybackTimingService : IPlaybackTimingService
{
    private const int MaxDelayChunkMs = 50;
    private const int CoarseSafetyMarginMs = 2;
    private const double FinalSpinWindowMs = 0.1;
    private const int YieldSpinInterval = 8;

    public async Task WaitAsync(int delayMs, IPlaybackPauseToken pauseToken, CancellationToken cancellationToken)
    {
        if (delayMs <= 0)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        long deadlineTicks = Stopwatch.GetTimestamp() + MillisecondsToTicks(delayMs);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (pauseToken.IsPaused)
            {
                var pauseStartTicks = Stopwatch.GetTimestamp();
                Log.Debug("[PlaybackTimingService] Pause detected during delay wait");
                await pauseToken.WaitIfPausedAsync(cancellationToken);
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
            if (remainingMs > CoarseSafetyMarginMs + 1)
            {
                int delaySliceMs = Math.Min(
                    MaxDelayChunkMs,
                    Math.Max(1, (int)Math.Floor(remainingMs) - CoarseSafetyMarginMs));
                await Task.Delay(delaySliceMs, cancellationToken);
                continue;
            }

            if (remainingMs > FinalSpinWindowMs)
            {
                await Task.Delay(1, cancellationToken);
                continue;
            }

            var spinner = new SpinWait();
            while (deadlineTicks - Stopwatch.GetTimestamp() > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (pauseToken.IsPaused)
                {
                    break;
                }

                spinner.SpinOnce(sleep1Threshold: -1);
                if (spinner.Count % YieldSpinInterval == 0)
                {
                    Thread.Yield();
                }
            }
        }
    }

    private static long MillisecondsToTicks(double milliseconds)
    {
        return (long)(milliseconds * Stopwatch.Frequency / 1000d);
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return ticks * 1000d / Stopwatch.Frequency;
    }
}
