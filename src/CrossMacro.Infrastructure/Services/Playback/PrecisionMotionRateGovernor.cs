namespace CrossMacro.Infrastructure.Services.Playback;

/// <summary>Limits Precision playback speed to preserve dense logical trajectories.</summary>
internal static class PrecisionMotionRateGovernor
{
    private const long MicrosecondsPerSecond = 1_000_000;
    private const long AnalysisWindowMicroseconds = 100_000;

    internal sealed record Plan(
        double RequestedSpeedMultiplier,
        double EffectiveSpeedMultiplier,
        double SourcePeakEventsPerSecond,
        int OutputCapEventsPerSecond)
    {
        public bool IsQualityLimited => EffectiveSpeedMultiplier < RequestedSpeedMultiplier;
    }

    public static Plan CreatePlan(MacroSequence macro, PlaybackOptions options)
    {
        ArgumentNullException.ThrowIfNull(macro);
        ArgumentNullException.ThrowIfNull(options);

        double requestedSpeed = PlaybackOptions.NormalizeSpeedMultiplier(options.SpeedMultiplier);
        int outputCap = PlaybackOptions.NormalizePrecisionMotionEventsPerSecond(
            options.PrecisionMotionEventsPerSecond);

        if (options.MotionMode is not MotionPlaybackMode.Precision)
        {
            return new Plan(requestedSpeed, requestedSpeed, 0, outputCap);
        }

        double sourcePeakEventsPerSecond = CalculateSourcePeakEventsPerSecond(macro);
        if (sourcePeakEventsPerSecond <= 0)
        {
            return new Plan(requestedSpeed, requestedSpeed, 0, outputCap);
        }

        double maximumPreservingSpeed = outputCap / sourcePeakEventsPerSecond;
        double effectiveSpeed = Math.Min(requestedSpeed, maximumPreservingSpeed);

        // Precision favors trajectory fidelity over requested duration.
        effectiveSpeed = Math.Max(PlaybackOptions.MinSpeedMultiplier, effectiveSpeed);

        return new Plan(requestedSpeed, effectiveSpeed, sourcePeakEventsPerSecond, outputCap);
    }

    private static double CalculateSourcePeakEventsPerSecond(MacroSequence macro)
    {
        long elapsedMicroseconds = 0;
        int peakSampleCount = 0;
        var sampleTimes = new Queue<long>();

        foreach (var ev in macro.Events)
        {
            try
            {
                elapsedMicroseconds = checked(elapsedMicroseconds + Math.Max(0, ev.DelayMicroseconds));
            }
            catch (OverflowException)
            {
                return 0;
            }

            if (!IsLogicalMotionMove(macro, ev))
            {
                continue;
            }

            sampleTimes.Enqueue(elapsedMicroseconds);
            while (sampleTimes.Count > 0
                   && elapsedMicroseconds - sampleTimes.Peek() >= AnalysisWindowMicroseconds)
            {
                _ = sampleTimes.Dequeue();
            }

            peakSampleCount = Math.Max(peakSampleCount, sampleTimes.Count);
        }

        return peakSampleCount * MicrosecondsPerSecond / (double)AnalysisWindowMicroseconds;
    }

    private static bool IsLogicalMotionMove(MacroSequence macro, MacroEvent ev)
    {
        return ev.Type is EventType.MouseMove
            && !ev.HasRandomDelay
            && MacroPositionSemantics.ResolveCoordinateMode(ev, macro.IsAbsoluteCoordinates) is MouseCoordinateMode.Absolute or MouseCoordinateMode.Relative
            && MacroPositionSemantics.ResolveCoordinateSpace(ev, macro.IsAbsoluteCoordinates) is MouseCoordinateSpace.LogicalDesktop;
    }
}
