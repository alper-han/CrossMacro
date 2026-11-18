using System.Diagnostics.CodeAnalysis;

namespace CrossMacro.Infrastructure.Services.Playback;

/// <summary>Builds bounded trajectories without owning the input device or playback lifetime.</summary>
internal static class MotionBatchPlanner
{
    private const int MaxDaemonTrajectorySamples = 128;
    private const long MaxDaemonTrajectoryDurationMicroseconds = 20_000;

    /// <summary>
    /// Plans a trajectory slice starting at <paramref name="startIndex"/>. Allocates only when the first
    /// event actually starts a slice, so non-batchable events cost nothing.
    /// </summary>
    public static bool TryCreate(
        MotionPlanningContext context,
        IList<MacroEvent> events,
        int startIndex,
        MacroSequence macro,
        double speedMultiplier,
        bool allowsCooperativeLogicalRelativeMovement,
        [NotNullWhen(true)] out List<AbsoluteMotionTrajectorySample>? samples,
        out (int X, int Y) finalLogicalTarget)
    {
        return TryCreateAbsoluteTrajectorySlice(
                   context,
                   events,
                   startIndex,
                   macro,
                   speedMultiplier,
                   out samples,
                   out finalLogicalTarget)
            || TryCreateLogicalRelativeTrajectorySlice(
                context,
                events,
                startIndex,
                macro,
                speedMultiplier,
                allowsCooperativeLogicalRelativeMovement,
                out samples,
                out finalLogicalTarget);
    }

    private static bool TryCreateAbsoluteTrajectorySlice(
        MotionPlanningContext context,
        IList<MacroEvent> events,
        int startIndex,
        MacroSequence macro,
        double speedMultiplier,
        [NotNullWhen(true)] out List<AbsoluteMotionTrajectorySample>? samples,
        out (int X, int Y) finalLogicalTarget)
    {
        samples = null;
        finalLogicalTarget = default;

        if (context.Simulator is not IAbsoluteMotionTrajectorySimulator
            || context.Executor is not { IsMouseButtonPressed: true } executor
            || startIndex >= events.Count)
        {
            return false;
        }

        List<AbsoluteMotionTrajectorySample>? slice = null;
        long totalDelayMicroseconds = 0;
        for (var index = startIndex; index < events.Count && (slice?.Count ?? 0) < MaxDaemonTrajectorySamples; index++)
        {
            var ev = events[index];
            if (ev.Type is not EventType.MouseMove
                || ev.HasRandomDelay
                || MacroPositionSemantics.ResolveCoordinateMode(ev, macro.IsAbsoluteCoordinates) is not MouseCoordinateMode.Absolute
                || MacroPositionSemantics.ResolveCoordinateSpace(ev, macro.IsAbsoluteCoordinates) is not MouseCoordinateSpace.LogicalDesktop)
            {
                break;
            }

            slice ??= CreateSliceBuffer(events.Count - startIndex);
            if (!TryAppendTrajectoryDelay(
                    slice,
                    ev.DelayMicroseconds,
                    speedMultiplier,
                    ref totalDelayMicroseconds))
            {
                break;
            }

            slice.Add(executor.CreateAbsoluteTrajectorySample(ev.X, ev.Y, delayAfterMicroseconds: 0));
            finalLogicalTarget = (ev.X, ev.Y);
        }

        samples = slice;
        return slice is { Count: > 1 };
    }

    private static bool TryCreateLogicalRelativeTrajectorySlice(
        MotionPlanningContext context,
        IList<MacroEvent> events,
        int startIndex,
        MacroSequence macro,
        double speedMultiplier,
        bool allowsCooperativeLogicalRelativeMovement,
        [NotNullWhen(true)] out List<AbsoluteMotionTrajectorySample>? samples,
        out (int X, int Y) finalLogicalTarget)
    {
        samples = null;
        finalLogicalTarget = default;

        var coordinator = context.Coordinator;
        var executor = context.Executor;
        if (allowsCooperativeLogicalRelativeMovement
            || context.Simulator is not IAbsoluteMotionTrajectorySimulator
            || coordinator is not { HasKnownPosition: true }
            || executor is null
            || startIndex >= events.Count)
        {
            return false;
        }

        List<AbsoluteMotionTrajectorySample>? slice = null;
        (int X, int Y) target = (coordinator.CurrentX, coordinator.CurrentY);
        long totalDelayMicroseconds = 0;
        for (var index = startIndex; index < events.Count && (slice?.Count ?? 0) < MaxDaemonTrajectorySamples; index++)
        {
            var ev = events[index];
            if (ev.Type is not EventType.MouseMove
                || ev.HasRandomDelay
                || MacroPositionSemantics.ResolveCoordinateMode(ev, macro.IsAbsoluteCoordinates) is not MouseCoordinateMode.Relative
                || MacroPositionSemantics.ResolveCoordinateSpace(ev, macro.IsAbsoluteCoordinates) is not MouseCoordinateSpace.LogicalDesktop)
            {
                break;
            }

            slice ??= CreateSliceBuffer(events.Count - startIndex);
            if (!TryAppendTrajectoryDelay(
                    slice,
                    ev.DelayMicroseconds,
                    speedMultiplier,
                    ref totalDelayMicroseconds))
            {
                break;
            }

            target = executor.ClampLogicalTarget((long)target.X + ev.X, (long)target.Y + ev.Y);
            slice.Add(executor.CreateAbsoluteTrajectorySample(target.X, target.Y, delayAfterMicroseconds: 0));
            finalLogicalTarget = target;
        }

        samples = slice;
        return slice is { Count: > 1 };
    }

    private static List<AbsoluteMotionTrajectorySample> CreateSliceBuffer(int remainingEvents) =>
        new(Math.Min(MaxDaemonTrajectorySamples, remainingEvents));

    private static bool TryAppendTrajectoryDelay(
        List<AbsoluteMotionTrajectorySample> samples,
        long sourceDelayMicroseconds,
        double speedMultiplier,
        ref long totalDelayMicroseconds)
    {
        if (samples.Count is 0)
        {
            return true;
        }

        long delayFromPreviousMicroseconds = ScaleDelayMicroseconds(sourceDelayMicroseconds, speedMultiplier);
        if (totalDelayMicroseconds + delayFromPreviousMicroseconds > MaxDaemonTrajectoryDurationMicroseconds)
        {
            return false;
        }

        var previousSample = samples[^1];
        samples[^1] = previousSample with { DelayAfterMicroseconds = delayFromPreviousMicroseconds };
        totalDelayMicroseconds += delayFromPreviousMicroseconds;
        return true;
    }

    private static long ScaleDelayMicroseconds(long sourceDelayMicroseconds, double speedMultiplier)
    {
        if (sourceDelayMicroseconds <= 0)
        {
            return 0;
        }

        return Math.Max(0, Convert.ToInt64(Math.Round(
            sourceDelayMicroseconds / speedMultiplier,
            MidpointRounding.AwayFromZero)));
    }
}
