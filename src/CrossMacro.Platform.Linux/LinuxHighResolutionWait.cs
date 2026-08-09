namespace CrossMacro.Platform.Linux;

/// <summary>Waits for Linux uinput batch timing with sub-millisecond precision.</summary>
internal static class LinuxHighResolutionWait
{
    private const long MicrosecondsPerMillisecond = 1_000;
    private const long FinalSpinWindowMicroseconds = 1_000;
    private const int MaximumCoarseSleepMilliseconds = 50;

    public static void Wait(long delayMicroseconds)
    {
        if (delayMicroseconds <= 0)
        {
            return;
        }

        var deadlineTicks = Stopwatch.GetTimestamp() + ToStopwatchTicks(delayMicroseconds);
        while (true)
        {
            var remainingTicks = deadlineTicks - Stopwatch.GetTimestamp();
            if (remainingTicks <= 0)
            {
                return;
            }

            var remainingMicroseconds = ToMicroseconds(remainingTicks);
            var coarseMicroseconds = remainingMicroseconds - FinalSpinWindowMicroseconds;
            if (coarseMicroseconds >= MicrosecondsPerMillisecond)
            {
                var coarseMilliseconds = (int)Math.Min(
                    MaximumCoarseSleepMilliseconds,
                    coarseMicroseconds / MicrosecondsPerMillisecond);
                Thread.Sleep(coarseMilliseconds);
                continue;
            }

            var spinner = new SpinWait();
            while (Stopwatch.GetTimestamp() < deadlineTicks)
            {
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
