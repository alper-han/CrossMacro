namespace CrossMacro.Daemon.Services;

/// <summary>Delays a bounded daemon input batch without millisecond quantization.</summary>
internal static class DaemonPrecisionDelay
{
    private const long MicrosecondsPerMillisecond = 1_000;
    private const long FinalSpinWindowMicroseconds = 1_000;

    public static async Task WaitAsync(long delayMicroseconds, CancellationToken cancellationToken)
    {
        if (delayMicroseconds <= 0)
        {
            return;
        }

        var deadlineTicks = Stopwatch.GetTimestamp() + ToStopwatchTicks(delayMicroseconds);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var remainingTicks = deadlineTicks - Stopwatch.GetTimestamp();
            if (remainingTicks <= 0)
            {
                return;
            }

            var remainingMicroseconds = ToMicroseconds(remainingTicks);
            var coarseMicroseconds = remainingMicroseconds - FinalSpinWindowMicroseconds;
            if (coarseMicroseconds >= MicrosecondsPerMillisecond)
            {
                await Task.Delay(
                    TimeSpan.FromMicroseconds(coarseMicroseconds),
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

    private static long ToStopwatchTicks(long microseconds) =>
        checked((long)Math.Ceiling(microseconds * Stopwatch.Frequency / 1_000_000d));

    private static long ToMicroseconds(long stopwatchTicks) =>
        checked((long)Math.Ceiling(stopwatchTicks * 1_000_000d / Stopwatch.Frequency));
}
