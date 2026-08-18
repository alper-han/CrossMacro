namespace CrossMacro.Infrastructure.Tests.Services.Playback;

public sealed class MotionTrajectoryResamplerTests
{
    [Fact]
    public void CreatePlan_PrecisionMode_PreservesEveryRecordedSample()
    {
        var macro = CreateDenseAbsoluteTrajectory();

        var plan = MotionTrajectoryResampler.CreatePlan(
            macro,
            speedMultiplier: 10,
            new PlaybackOptions { MotionMode = MotionPlaybackMode.Precision });

        _ = plan.Events.Should().Equal(macro.Events);
        _ = plan.ResampledSegmentCount.Should().Be(0);
        _ = plan.OmittedSampleCount.Should().Be(0);
    }

    [Fact]
    public void CreatePlan_StrictSpeedMode_InterpolatesTrajectoryAtTheBoundedOutputRate()
    {
        var macro = CreateDenseAbsoluteTrajectory();

        var plan = MotionTrajectoryResampler.CreatePlan(
            macro,
            speedMultiplier: 1,
            new PlaybackOptions
            {
                MotionMode = MotionPlaybackMode.StrictSpeed,
                StrictSpeedMotionEventsPerSecond = 100,
            });

        _ = plan.Events.Select(static ev => (ev.X, ev.Y)).Should().Equal((0, 0), (100, 0), (200, 0), (250, 0));
        _ = plan.Events.Select(static ev => ev.DelayMicroseconds).Should().Equal(0, 10_000, 10_000, 5_000);
        _ = plan.Events.Select(static ev => ev.TimestampMicroseconds).Should().Equal(0, 10_000, 20_000, 25_000);
        _ = plan.ResampledSegmentCount.Should().Be(1);
        _ = plan.OmittedSampleCount.Should().Be(0);
        _ = plan.IsErrorBoundSatisfied.Should().BeTrue();
    }

    [Fact]
    public void CreatePlan_StrictSpeedMode_ReportsWhenFixedRateCannotRetainALoopWithinThePixelBudget()
    {
        var macro = new MacroSequence { IsAbsoluteCoordinates = true };
        macro.Events.Add(CreateMove(0, 0, 0));
        macro.Events.Add(new MacroEvent { Type = EventType.MouseMove, X = 0, Y = 40, TimestampMicroseconds = 5_000, DelayMicroseconds = 5_000 });
        macro.Events.Add(new MacroEvent { Type = EventType.MouseMove, X = 40, Y = 40, TimestampMicroseconds = 10_000, DelayMicroseconds = 5_000 });
        macro.Events.Add(new MacroEvent { Type = EventType.MouseMove, X = 40, Y = 0, TimestampMicroseconds = 15_000, DelayMicroseconds = 5_000 });
        macro.Events.Add(new MacroEvent { Type = EventType.MouseMove, X = 0, Y = 0, TimestampMicroseconds = 20_000, DelayMicroseconds = 5_000 });

        var plan = MotionTrajectoryResampler.CreatePlan(
            macro,
            speedMultiplier: 1,
            new PlaybackOptions
            {
                MotionMode = MotionPlaybackMode.StrictSpeed,
                StrictSpeedMotionEventsPerSecond = 60,
                MaximumMotionErrorPixels = 1d,
            });

        _ = plan.IsErrorBoundSatisfied.Should().BeFalse();
        _ = plan.MaximumGeometricErrorPixels.Should().BeGreaterThan(1d);
    }

    [Fact]
    public void CreatePlan_StrictSpeedMode_ResamplesLogicalRelativeMovesWithoutDroppingTheInitialDelta()
    {
        var macro = new MacroSequence { IsAbsoluteCoordinates = false };
        for (int index = 0; index < 5; index++)
        {
            macro.Events.Add(new MacroEvent
            {
                Type = EventType.MouseMove,
                X = 10,
                Y = 0,
                CoordinateMode = MouseCoordinateMode.Relative,
                CoordinateSpace = MouseCoordinateSpace.LogicalDesktop,
                TimestampMicroseconds = index * 5_000,
                DelayMicroseconds = index is 0 ? 0 : 5_000,
            });
        }

        var plan = MotionTrajectoryResampler.CreatePlan(
            macro,
            speedMultiplier: 1,
            new PlaybackOptions
            {
                MotionMode = MotionPlaybackMode.StrictSpeed,
                StrictSpeedMotionEventsPerSecond = 100,
            });

        _ = plan.Events.Select(static ev => (ev.X, ev.Y)).Should().Equal((10, 0), (20, 0), (20, 0));
        _ = plan.Events.Select(static ev => ev.DelayMicroseconds).Should().Equal(0, 10_000, 10_000);
        _ = plan.Events.Select(static ev => ev.CoordinateMode).Should().OnlyContain(
            mode => mode == MouseCoordinateMode.Relative);
        _ = plan.Events.Select(static ev => ev.CoordinateSpace).Should().OnlyContain(
            space => space == MouseCoordinateSpace.LogicalDesktop);
    }

    private static MacroSequence CreateDenseAbsoluteTrajectory()
    {
        return new MacroSequence
        {
            IsAbsoluteCoordinates = true,
            Events =
            {
                CreateMove(x: 0, timestampMicroseconds: 0, delayMicroseconds: 0),
                CreateMove(x: 120, timestampMicroseconds: 12_000, delayMicroseconds: 12_000),
                CreateMove(x: 240, timestampMicroseconds: 24_000, delayMicroseconds: 12_000),
                CreateMove(x: 250, timestampMicroseconds: 25_000, delayMicroseconds: 1_000),
            },
        };
    }

    private static MacroEvent CreateMove(int x, long timestampMicroseconds, long delayMicroseconds)
    {
        return new MacroEvent
        {
            Type = EventType.MouseMove,
            X = x,
            Y = 0,
            Timestamp = MacroTiming.ToLegacyTimestampMilliseconds(timestampMicroseconds),
            TimestampMicroseconds = timestampMicroseconds,
            DelayMs = MacroTiming.ToLegacyMilliseconds(delayMicroseconds),
            DelayMicroseconds = delayMicroseconds,
        };
    }
}
