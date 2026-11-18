
namespace CrossMacro.Core.Tests.Models;

public sealed class PlaybackOptionsTests
{
    [Fact]
    public void NewPlaybackOptions_HasCorrectDefaultValues()
    {
        var options = new PlaybackOptions();

        _ = options.SpeedMultiplier.Should().Be(1.0);
        _ = options.Loop.Should().BeFalse();
        _ = options.RepeatCount.Should().Be(1);
        _ = options.RepeatDelayMs.Should().Be(0);
        _ = options.UseRandomRepeatDelay.Should().BeFalse();
        _ = options.RepeatDelayMinMs.Should().Be(0);
        _ = options.RepeatDelayMaxMs.Should().Be(0);
        _ = options.MotionMode.Should().Be(MotionPlaybackMode.Precision);
        _ = options.StrictSpeedMotionEventsPerSecond.Should().Be(1_000);
        _ = options.PrecisionMotionEventsPerSecond.Should().Be(300);
        _ = options.MaximumMotionErrorPixels.Should().Be(2d);
    }

    [Theory]
    [InlineData(-5.0, 0.1)]
    [InlineData(0.0, 0.1)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(2.0)]
    [InlineData(10.0)]
    [InlineData(25.0, 10.0)]
    public void PlaybackOptions_NormalizesSpeedMultipliers(double speed, double? expected = null)
    {
        var options = new PlaybackOptions
        {
            SpeedMultiplier = speed,
        };
        options.Normalize();

        _ = options.SpeedMultiplier.Should().Be(expected ?? speed);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void PlaybackOptions_NormalizeSpeedMultiplier_WhenValueIsNotFinite_UsesDefault(double speed)
    {
        var normalized = PlaybackOptions.NormalizeSpeedMultiplier(speed);

        _ = normalized.Should().Be(PlaybackOptions.DefaultSpeedMultiplier);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(25, 25)]
    public void PlaybackOptions_NormalizeDelayMs_ClampsNegativeValues(int value, int expected)
    {
        _ = PlaybackOptions.NormalizeDelayMs(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(-5, -1, 0, 0)]
    [InlineData(300, 100, 300, 300)]
    [InlineData(100, 300, 100, 300)]
    public void PlaybackOptions_NormalizeDelayRange_ClampsAndOrdersValues(
        int min,
        int max,
        int expectedMin,
        int expectedMax)
    {
        _ = PlaybackOptions.NormalizeDelayRange(min, max).Should().Be((expectedMin, expectedMax));
    }

    [Theory]
    [InlineData(0, 1_000)]
    [InlineData(-1, 1_000)]
    [InlineData(1, 1)]
    [InlineData(10_000, 10_000)]
    [InlineData(10_001, 10_000)]
    public void PlaybackOptions_NormalizeStrictSpeedRate_ClampsToSupportedRange(int value, int expected)
    {
        _ = PlaybackOptions.NormalizeStrictSpeedMotionEventsPerSecond(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(0, 300)]
    [InlineData(-1, 300)]
    [InlineData(1, 1)]
    [InlineData(10_000, 10_000)]
    [InlineData(10_001, 10_000)]
    public void PlaybackOptions_NormalizePrecisionRate_ClampsToSupportedRange(int value, int expected)
    {
        _ = PlaybackOptions.NormalizePrecisionMotionEventsPerSecond(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(0, 2d)]
    [InlineData(0.1, 0.25)]
    [InlineData(0.25, 0.25)]
    [InlineData(500, 500d)]
    [InlineData(600, 500d)]
    [InlineData(double.NaN, 2d)]
    [InlineData(double.PositiveInfinity, 2d)]
    [InlineData(double.NegativeInfinity, 2d)]
    public void PlaybackOptions_NormalizeMaximumMotionError_ClampsAndRejectsInvalidValues(
        double value,
        double expected)
    {
        _ = PlaybackOptions.NormalizeMaximumMotionErrorPixels(value).Should().Be(expected);
    }
}
