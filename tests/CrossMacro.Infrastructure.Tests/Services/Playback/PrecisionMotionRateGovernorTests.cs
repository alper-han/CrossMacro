namespace CrossMacro.Infrastructure.Tests.Services.Playback;

public sealed class PrecisionMotionRateGovernorTests
{
    [Fact]
    public void CreatePlan_PrecisionMode_LowersEffectiveSpeedBeforeItExceedsTheQualityCeiling()
    {
        var macro = CreateAbsoluteMoves(sampleCount: 20, delayMicroseconds: 5_000);

        var plan = PrecisionMotionRateGovernor.CreatePlan(
            macro,
            new PlaybackOptions
            {
                MotionMode = MotionPlaybackMode.Precision,
                SpeedMultiplier = 10,
                PrecisionMotionEventsPerSecond = 300,
            });

        _ = plan.SourcePeakEventsPerSecond.Should().Be(200);
        _ = plan.RequestedSpeedMultiplier.Should().Be(10);
        _ = plan.EffectiveSpeedMultiplier.Should().Be(1.5);
        _ = plan.IsQualityLimited.Should().BeTrue();
    }

    [Fact]
    public void CreatePlan_PrecisionMode_PreservesRequestedSpeedWhenTheQualityCeilingIsNotExceeded()
    {
        var macro = CreateAbsoluteMoves(sampleCount: 10, delayMicroseconds: 10_000);

        var plan = PrecisionMotionRateGovernor.CreatePlan(
            macro,
            new PlaybackOptions
            {
                MotionMode = MotionPlaybackMode.Precision,
                SpeedMultiplier = 2,
                PrecisionMotionEventsPerSecond = 300,
            });

        _ = plan.SourcePeakEventsPerSecond.Should().Be(100);
        _ = plan.EffectiveSpeedMultiplier.Should().Be(2);
        _ = plan.IsQualityLimited.Should().BeFalse();
    }

    [Fact]
    public void CreatePlan_PrecisionMode_LimitsLogicalRelativeMoves()
    {
        var macro = new MacroSequence { IsAbsoluteCoordinates = false };
        for (int index = 0; index < 20; index++)
        {
            macro.Events.Add(new MacroEvent
            {
                Type = EventType.MouseMove,
                X = 1,
                Y = 0,
                CoordinateMode = MouseCoordinateMode.Relative,
                CoordinateSpace = MouseCoordinateSpace.LogicalDesktop,
                TimestampMicroseconds = index * 5_000,
                DelayMicroseconds = index is 0 ? 0 : 5_000,
            });
        }

        var plan = PrecisionMotionRateGovernor.CreatePlan(
            macro,
            new PlaybackOptions
            {
                MotionMode = MotionPlaybackMode.Precision,
                SpeedMultiplier = 10,
                PrecisionMotionEventsPerSecond = 300,
            });

        _ = plan.SourcePeakEventsPerSecond.Should().Be(200);
        _ = plan.EffectiveSpeedMultiplier.Should().Be(1.5);
    }

    [Fact]
    public void CreatePlan_StrictSpeedMode_DoesNotChangeTheRequestedSpeed()
    {
        var macro = CreateAbsoluteMoves(sampleCount: 20, delayMicroseconds: 5_000);

        var plan = PrecisionMotionRateGovernor.CreatePlan(
            macro,
            new PlaybackOptions
            {
                MotionMode = MotionPlaybackMode.StrictSpeed,
                SpeedMultiplier = 10,
                PrecisionMotionEventsPerSecond = 300,
            });

        _ = plan.SourcePeakEventsPerSecond.Should().Be(0);
        _ = plan.EffectiveSpeedMultiplier.Should().Be(10);
        _ = plan.IsQualityLimited.Should().BeFalse();
    }

    private static MacroSequence CreateAbsoluteMoves(int sampleCount, long delayMicroseconds)
    {
        var macro = new MacroSequence { IsAbsoluteCoordinates = true };
        for (int index = 0; index < sampleCount; index++)
        {
            macro.Events.Add(new MacroEvent
            {
                Type = EventType.MouseMove,
                X = index,
                Y = 0,
                TimestampMicroseconds = index * delayMicroseconds,
                DelayMicroseconds = index is 0 ? 0 : delayMicroseconds,
            });
        }

        return macro;
    }
}
