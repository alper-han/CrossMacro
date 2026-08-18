namespace CrossMacro.Infrastructure.Services.Playback;

/// <summary>Builds a bounded duration-first trajectory for StrictSpeed playback.</summary>
internal static class MotionTrajectoryResampler
{
    private const long MicrosecondsPerSecond = 1_000_000;
    private const long MaximumAdaptiveErrorEvaluations = 4_000_000;

    private enum MotionSegmentKind
    {
        Absolute,
        LogicalRelative,
    }

    internal readonly record struct Plan(
        IList<MacroEvent> Events,
        int ResampledSegmentCount,
        int OmittedSampleCount,
        int AdaptiveAnchorReplacementCount,
        double MaximumGeometricErrorPixels,
        bool IsErrorBoundSatisfied)
    {
        public static Plan Unchanged(IList<MacroEvent> events) => new(
            events,
            ResampledSegmentCount: 0,
            OmittedSampleCount: 0,
            AdaptiveAnchorReplacementCount: 0,
            MaximumGeometricErrorPixels: 0d,
            IsErrorBoundSatisfied: true);
    }

    public static Plan CreatePlan(
        MacroSequence macro,
        double speedMultiplier,
        PlaybackOptions options)
    {
        ArgumentNullException.ThrowIfNull(macro);
        ArgumentNullException.ThrowIfNull(options);

        if (options.MotionMode is not MotionPlaybackMode.StrictSpeed || macro.Events.Count < 2)
        {
            return Plan.Unchanged(macro.Events);
        }

        int maximumEventsPerSecond = PlaybackOptions.NormalizeStrictSpeedMotionEventsPerSecond(
            options.StrictSpeedMotionEventsPerSecond);
        double maximumErrorPixels = PlaybackOptions.NormalizeMaximumMotionErrorPixels(
            options.MaximumMotionErrorPixels);
        long sourceIntervalMicroseconds = Math.Max(
            1,
            (long)Math.Round(
                speedMultiplier * MicrosecondsPerSecond / maximumEventsPerSecond,
                MidpointRounding.AwayFromZero));

        var output = new List<MacroEvent>(macro.Events.Count);
        int resampledSegmentCount = 0;
        int omittedSampleCount = 0;
        int adaptiveAnchorReplacementCount = 0;
        double maximumGeometricErrorPixels = 0d;
        bool isErrorBoundSatisfied = true;

        for (int index = 0; index < macro.Events.Count;)
        {
            if (!TryGetMotionSegmentKind(macro, macro.Events[index], out var segmentKind))
            {
                output.Add(macro.Events[index]);
                index++;
                continue;
            }

            int segmentEnd = FindSegmentEnd(macro, index, segmentKind);
            IList<MacroEvent> sourceEvents = segmentKind is MotionSegmentKind.LogicalRelative
                ? CreateCumulativeLogicalRelativeEvents(macro.Events, index, segmentEnd)
                : macro.Events;
            int sourceStart = segmentKind is MotionSegmentKind.LogicalRelative ? 0 : index;
            int sourceEnd = segmentKind is MotionSegmentKind.LogicalRelative
                ? sourceEvents.Count - 1
                : segmentEnd;
            if (segmentEnd == index || !TryGetTotalDuration(sourceEvents, sourceStart, sourceEnd, out long durationMicroseconds)
                || durationMicroseconds >= checked((long)(segmentEnd - index) * sourceIntervalMicroseconds))
            {
                for (int current = index; current <= segmentEnd; current++)
                {
                    output.Add(macro.Events[current]);
                }

                index = segmentEnd + 1;
                continue;
            }

            var segment = CreateTimePacedSegment(
                sourceEvents,
                sourceStart,
                sourceEnd,
                durationMicroseconds,
                sourceIntervalMicroseconds);
            var adaptive = ImproveAnchorsWithinFixedBudget(
                sourceEvents,
                sourceStart,
                sourceEnd,
                sourceIntervalMicroseconds,
                segment,
                maximumErrorPixels);

            if (segmentKind is MotionSegmentKind.LogicalRelative)
            {
                RestoreLogicalRelativeDeltas(segment);
            }

            output.AddRange(segment);
            resampledSegmentCount++;
            omittedSampleCount += segmentEnd - index + 1 - segment.Count;
            adaptiveAnchorReplacementCount += adaptive.ReplacementCount;
            maximumGeometricErrorPixels = Math.Max(maximumGeometricErrorPixels, adaptive.MaximumErrorPixels);
            isErrorBoundSatisfied &= adaptive.IsErrorBoundSatisfied;
            index = segmentEnd + 1;
        }

        return new Plan(
            output,
            resampledSegmentCount,
            omittedSampleCount,
            adaptiveAnchorReplacementCount,
            maximumGeometricErrorPixels,
            isErrorBoundSatisfied);
    }

    private static bool TryGetMotionSegmentKind(
        MacroSequence macro,
        MacroEvent ev,
        out MotionSegmentKind segmentKind)
    {
        segmentKind = default;
        if (ev.Type is not EventType.MouseMove || ev.HasRandomDelay)
        {
            return false;
        }

        var coordinateMode = MacroPositionSemantics.ResolveCoordinateMode(ev, macro.IsAbsoluteCoordinates);
        if (coordinateMode is MouseCoordinateMode.Absolute)
        {
            segmentKind = MotionSegmentKind.Absolute;
            return true;
        }

        if (coordinateMode is MouseCoordinateMode.Relative
            && MacroPositionSemantics.ResolveCoordinateSpace(ev, macro.IsAbsoluteCoordinates) is MouseCoordinateSpace.LogicalDesktop)
        {
            segmentKind = MotionSegmentKind.LogicalRelative;
            return true;
        }

        return false;
    }

    private static int FindSegmentEnd(MacroSequence macro, int start, MotionSegmentKind segmentKind)
    {
        int end = start;
        while (end + 1 < macro.Events.Count
               && TryGetMotionSegmentKind(macro, macro.Events[end + 1], out var nextKind)
               && nextKind == segmentKind)
        {
            end++;
        }

        return end;
    }

    private static List<MacroEvent> CreateCumulativeLogicalRelativeEvents(
        IList<MacroEvent> events,
        int start,
        int end)
    {
        var cumulativeEvents = new List<MacroEvent>(end - start + 1);
        long currentX = 0;
        long currentY = 0;
        for (int index = start; index <= end; index++)
        {
            var current = events[index];
            currentX = Math.Clamp(currentX + current.X, int.MinValue, int.MaxValue);
            currentY = Math.Clamp(currentY + current.Y, int.MinValue, int.MaxValue);
            current.X = (int)currentX;
            current.Y = (int)currentY;
            cumulativeEvents.Add(current);
        }

        return cumulativeEvents;
    }

    private static void RestoreLogicalRelativeDeltas(IList<MacroEvent> events)
    {
        long previousX = 0;
        long previousY = 0;
        for (int index = 0; index < events.Count; index++)
        {
            var current = events[index];
            long positionX = current.X;
            long positionY = current.Y;
            current.X = (int)Math.Clamp(positionX - previousX, int.MinValue, int.MaxValue);
            current.Y = (int)Math.Clamp(positionY - previousY, int.MinValue, int.MaxValue);
            events[index] = current;
            previousX = positionX;
            previousY = positionY;
        }
    }

    private static bool TryGetTotalDuration(
        IList<MacroEvent> events,
        int start,
        int end,
        out long durationMicroseconds)
    {
        durationMicroseconds = 0;
        try
        {
            for (int index = start + 1; index <= end; index++)
            {
                durationMicroseconds = checked(durationMicroseconds + events[index].DelayMicroseconds);
            }

            return durationMicroseconds > 0;
        }
        catch (OverflowException)
        {
            durationMicroseconds = 0;
            return false;
        }
    }

    private static List<MacroEvent> CreateTimePacedSegment(
        IList<MacroEvent> events,
        int start,
        int end,
        long durationMicroseconds,
        long sourceIntervalMicroseconds)
    {
        var output = new List<MacroEvent>();
        var first = events[start];
        long firstTimestampMicroseconds = first.TimestampMicroseconds;
        long firstDelayMicroseconds = first.DelayMicroseconds;
        long previousOutputTimeMicroseconds = 0;

        SetTiming(
            ref first,
            timestampMicroseconds: firstTimestampMicroseconds,
            delayMicroseconds: firstDelayMicroseconds);
        output.Add(first);

        for (long sampleTime = sourceIntervalMicroseconds;
             sampleTime < durationMicroseconds;
             sampleTime = checked(sampleTime + sourceIntervalMicroseconds))
        {
            var sample = Interpolate(events, start, end, sampleTime);
            SetTiming(
                ref sample,
                timestampMicroseconds: checked(firstTimestampMicroseconds + sampleTime),
                delayMicroseconds: checked(sampleTime - previousOutputTimeMicroseconds));
            output.Add(sample);
            previousOutputTimeMicroseconds = sampleTime;
        }

        var final = events[end];
        SetTiming(
            ref final,
            timestampMicroseconds: checked(firstTimestampMicroseconds + durationMicroseconds),
            delayMicroseconds: checked(durationMicroseconds - previousOutputTimeMicroseconds));
        output.Add(final);
        return output;
    }

    private static AdaptiveResult ImproveAnchorsWithinFixedBudget(
        IList<MacroEvent> original,
        int start,
        int end,
        long sourceIntervalMicroseconds,
        IList<MacroEvent> resampled,
        double maximumAllowedErrorPixels)
    {
        if (resampled.Count is 0)
        {
            return new AdaptiveResult(
                MaximumErrorPixels: double.PositiveInfinity,
                ReplacementCount: 0,
                IsErrorBoundSatisfied: false);
        }

        var sourceTimes = GetSourceTimes(original, start, end);
        var protectedIndices = new HashSet<int> { 0, resampled.Count - 1 };
        int replacements = 0;
        int replacementBudget = Math.Max(0, resampled.Count - protectedIndices.Count);
        long remainingEvaluations = MaximumAdaptiveErrorEvaluations;

        while (replacements < replacementBudget && remainingEvaluations > 0)
        {
            var worst = FindWorstSourcePoint(
                original,
                start,
                end,
                sourceTimes,
                sourceIntervalMicroseconds,
                resampled,
                remainingEvaluations);
            remainingEvaluations -= worst.InspectedPointCount;

            if (!worst.FullyInspected)
            {
                return new AdaptiveResult(
                    MaximumErrorPixels: double.PositiveInfinity,
                    ReplacementCount: replacements,
                    IsErrorBoundSatisfied: false);
            }

            if (worst.ErrorPixels <= maximumAllowedErrorPixels)
            {
                return new AdaptiveResult(
                    MaximumErrorPixels: worst.ErrorPixels,
                    ReplacementCount: replacements,
                    IsErrorBoundSatisfied: true);
            }

            int outputIndex = FindNearestOutputIndex(
                sourceTimes[worst.SourceOffset],
                sourceIntervalMicroseconds,
                resampled.Count,
                protectedIndices);
            if (outputIndex < 0)
            {
                break;
            }

            var anchor = original[start + worst.SourceOffset];
            var destination = resampled[outputIndex];
            destination.X = anchor.X;
            destination.Y = anchor.Y;
            resampled[outputIndex] = destination;
            protectedIndices.Add(outputIndex);
            replacements++;
        }

        if (remainingEvaluations <= 0)
        {
            return new AdaptiveResult(
                MaximumErrorPixels: double.PositiveInfinity,
                ReplacementCount: replacements,
                IsErrorBoundSatisfied: false);
        }

        var finalWorst = FindWorstSourcePoint(
            original,
            start,
            end,
            sourceTimes,
            sourceIntervalMicroseconds,
            resampled,
            remainingEvaluations);
        return new AdaptiveResult(
            MaximumErrorPixels: finalWorst.FullyInspected
                ? finalWorst.ErrorPixels
                : double.PositiveInfinity,
            ReplacementCount: replacements,
            IsErrorBoundSatisfied: finalWorst.FullyInspected
                && finalWorst.ErrorPixels <= maximumAllowedErrorPixels);
    }

    private static long[] GetSourceTimes(IList<MacroEvent> original, int start, int end)
    {
        var sourceTimes = new long[end - start + 1];
        long elapsed = 0;
        sourceTimes[0] = 0;
        for (var index = 1; index < sourceTimes.Length; index++)
        {
            elapsed = checked(elapsed + Math.Max(0, original[start + index].DelayMicroseconds));
            sourceTimes[index] = elapsed;
        }

        return sourceTimes;
    }

    private static WorstSourcePoint FindWorstSourcePoint(
        IList<MacroEvent> original,
        int start,
        int end,
        IReadOnlyList<long> sourceTimes,
        long sourceIntervalMicroseconds,
        IList<MacroEvent> resampled,
        long evaluationBudget)
    {
        var worst = new WorstSourcePoint(0, 0d);
        int sourcePointCount = end - start + 1;
        int maximumInspections = (int)Math.Min(
            sourcePointCount,
            Math.Max(1L, evaluationBudget));
        int stride = sourcePointCount <= maximumInspections
            ? 1
            : (int)Math.Ceiling(sourcePointCount / (double)maximumInspections);
        int inspectedPointCount = 0;

        for (long candidate = 0; candidate < sourcePointCount; candidate += stride)
        {
            int sourceIndex = (int)candidate;
            var error = CalculateTimeCorrespondingError(
                original[start + sourceIndex],
                sourceTimes[sourceIndex],
                sourceIntervalMicroseconds,
                resampled);
            if (error > worst.ErrorPixels)
            {
                worst = new WorstSourcePoint(sourceIndex, error);
            }

            inspectedPointCount++;
        }

        int lastSourceIndex = sourcePointCount - 1;
        if (lastSourceIndex % stride is not 0)
        {
            var error = CalculateTimeCorrespondingError(
                original[start + lastSourceIndex],
                sourceTimes[lastSourceIndex],
                sourceIntervalMicroseconds,
                resampled);
            if (error > worst.ErrorPixels)
            {
                worst = new WorstSourcePoint(lastSourceIndex, error);
            }

            inspectedPointCount++;
        }

        return worst with
        {
            InspectedPointCount = inspectedPointCount,
            FullyInspected = stride is 1,
        };
    }

    private static double CalculateTimeCorrespondingError(
        MacroEvent sourcePoint,
        long sourceTimeMicroseconds,
        long sourceIntervalMicroseconds,
        IList<MacroEvent> resampled)
    {
        if (resampled.Count is 0)
        {
            return double.PositiveInfinity;
        }

        if (resampled.Count is 1)
        {
            return Math.Sqrt(DistanceSquared(sourcePoint, resampled[0]));
        }

        int lowerIndex = (int)Math.Clamp(
            sourceTimeMicroseconds / sourceIntervalMicroseconds,
            0L,
            resampled.Count - 2L);
        return Math.Sqrt(DistanceSquaredToSegment(
            sourcePoint,
            resampled[lowerIndex],
            resampled[lowerIndex + 1]));
    }

    private static int FindNearestOutputIndex(
        long sourceTimeMicroseconds,
        long sourceIntervalMicroseconds,
        int outputCount,
        ISet<int> protectedIndices)
    {
        if (outputCount <= 2)
        {
            return -1;
        }

        var preferred = (int)Math.Clamp(
            Math.Round(sourceTimeMicroseconds / (double)sourceIntervalMicroseconds, MidpointRounding.AwayFromZero),
            1d,
            outputCount - 2d);
        for (var offset = 0; offset < outputCount - 2; offset++)
        {
            var before = preferred - offset;
            if (before >= 1 && !protectedIndices.Contains(before))
            {
                return before;
            }

            var after = preferred + offset;
            if (after <= outputCount - 2 && !protectedIndices.Contains(after))
            {
                return after;
            }
        }

        return -1;
    }

    private static MacroEvent Interpolate(
        IList<MacroEvent> events,
        int start,
        int end,
        long sampleTimeMicroseconds)
    {
        long segmentStartTime = 0;
        for (int upperIndex = start + 1; upperIndex <= end; upperIndex++)
        {
            long segmentEndTime = checked(segmentStartTime + events[upperIndex].DelayMicroseconds);
            if (sampleTimeMicroseconds <= segmentEndTime && segmentEndTime > segmentStartTime)
            {
                var lower = events[upperIndex - 1];
                var upper = events[upperIndex];
                double ratio = (sampleTimeMicroseconds - segmentStartTime)
                    / (double)(segmentEndTime - segmentStartTime);
                upper.X = InterpolateAxis(lower.X, upper.X, ratio);
                upper.Y = InterpolateAxis(lower.Y, upper.Y, ratio);
                return upper;
            }

            segmentStartTime = segmentEndTime;
        }

        return events[end];
    }

    private static double DistanceSquaredToSegment(MacroEvent point, MacroEvent start, MacroEvent end)
    {
        var dx = (double)end.X - start.X;
        var dy = (double)end.Y - start.Y;
        var lengthSquared = (dx * dx) + (dy * dy);
        if (lengthSquared <= double.Epsilon)
        {
            return DistanceSquared(point, start);
        }

        var projection = ((((double)point.X - start.X) * dx) + (((double)point.Y - start.Y) * dy)) / lengthSquared;
        projection = Math.Clamp(projection, 0d, 1d);
        var errorX = point.X - (start.X + (projection * dx));
        var errorY = point.Y - (start.Y + (projection * dy));
        return (errorX * errorX) + (errorY * errorY);
    }

    private static double DistanceSquared(MacroEvent left, MacroEvent right)
    {
        var dx = (double)left.X - right.X;
        var dy = (double)left.Y - right.Y;
        return (dx * dx) + (dy * dy);
    }

    private static int InterpolateAxis(int start, int end, double ratio)
    {
        double value = start + (((double)end - start) * ratio);
        return checked((int)Math.Round(value, MidpointRounding.AwayFromZero));
    }

    private static void SetTiming(ref MacroEvent ev, long timestampMicroseconds, long delayMicroseconds)
    {
        ev.TimestampMicroseconds = timestampMicroseconds;
        ev.DelayMicroseconds = delayMicroseconds;
    }

    private sealed record WorstSourcePoint(
        int SourceOffset,
        double ErrorPixels,
        int InspectedPointCount = 0,
        bool FullyInspected = false);

    private readonly record struct AdaptiveResult(
        double MaximumErrorPixels,
        int ReplacementCount,
        bool IsErrorBoundSatisfied);
}
