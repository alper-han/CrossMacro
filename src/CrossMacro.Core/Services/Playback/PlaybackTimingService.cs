using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Logging;

namespace CrossMacro.Core.Services.Playback;

/// <summary>
/// High-precision timing service for playback delays.
/// Uses spin-wait for small delays, Task.Delay for larger ones.
/// </summary>
public class PlaybackTimingService : IPlaybackTimingService
{
    private const int SmallDelayThresholdMs = 15;
    private const int CheckIntervalMs = 50;

    public async Task WaitAsync(int delayMs, IPlaybackPauseToken pauseToken, CancellationToken cancellationToken)
    {
        if (delayMs <= 0)
            return;

        int remaining = delayMs;

        while (remaining > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Check for pause state
            if (pauseToken.IsPaused)
            {
                Log.Debug("[PlaybackTimingService] Pause detected, {Remaining}ms remaining", remaining);
                await pauseToken.WaitIfPausedAsync(cancellationToken);
                Log.Debug("[PlaybackTimingService] Resumed, continuing with {Remaining}ms delay", remaining);
            }

            int waitTime = Math.Min(remaining, CheckIntervalMs);

            if (waitTime > SmallDelayThresholdMs)
            {
                // Use Task.Delay for larger waits
                await Task.Delay(waitTime, cancellationToken);
            }
            else if (waitTime > 0)
            {
                // Use spin-wait for precise small delays
                SpinWait(waitTime, pauseToken, cancellationToken);
            }

            // Ensure cancellation is checked after spin-wait
            cancellationToken.ThrowIfCancellationRequested();

            remaining -= waitTime;
        }
    }

    private static void SpinWait(int milliseconds, IPlaybackPauseToken pauseToken, CancellationToken cancellationToken)
    {
        long startTicks = Stopwatch.GetTimestamp();
        long targetTicks = startTicks + (long)(milliseconds * Stopwatch.Frequency / 1000.0);

        while (Stopwatch.GetTimestamp() < targetTicks)
        {
            if (milliseconds > 1)
                Thread.SpinWait(100);
            else
                Thread.Yield();

            // Check for pause and cancellation during spin-wait
            if (pauseToken.IsPaused || cancellationToken.IsCancellationRequested)
                break;
        }
    }
}
