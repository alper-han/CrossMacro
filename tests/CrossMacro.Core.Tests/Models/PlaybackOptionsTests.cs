namespace CrossMacro.Core.Tests.Models;

using CrossMacro.Core.Models;
using FluentAssertions;

public class PlaybackOptionsTests
{
    [Fact]
    public void NewPlaybackOptions_HasCorrectDefaultValues()
    {
        // Arrange & Act
        var options = new PlaybackOptions();

        // Assert
        options.SpeedMultiplier.Should().Be(1.0);
        options.Loop.Should().BeFalse();
        options.RepeatCount.Should().Be(1);
        options.RepeatDelayMs.Should().Be(0);
    }

    [Fact]
    public void PlaybackOptions_CanSetSpeedMultiplier()
    {
        // Arrange
        var options = new PlaybackOptions();

        // Act
        options.SpeedMultiplier = 0.5;

        // Assert
        options.SpeedMultiplier.Should().Be(0.5);
    }

    [Fact]
    public void PlaybackOptions_CanSetDoubleSpeed()
    {
        // Arrange
        var options = new PlaybackOptions();

        // Act
        options.SpeedMultiplier = 2.0;

        // Assert
        options.SpeedMultiplier.Should().Be(2.0);
    }

    [Fact]
    public void PlaybackOptions_CanEnableLoop()
    {
        // Arrange
        var options = new PlaybackOptions();

        // Act
        options.Loop = true;

        // Assert
        options.Loop.Should().BeTrue();
    }

    [Fact]
    public void PlaybackOptions_CanSetRepeatCount()
    {
        // Arrange
        var options = new PlaybackOptions();

        // Act
        options.RepeatCount = 5;

        // Assert
        options.RepeatCount.Should().Be(5);
    }

    [Fact]
    public void PlaybackOptions_ZeroRepeatCountMeansInfinite()
    {
        // Arrange
        var options = new PlaybackOptions();

        // Act
        options.Loop = true;
        options.RepeatCount = 0;

        // Assert - 0 means infinite when Loop is true
        options.RepeatCount.Should().Be(0);
        options.Loop.Should().BeTrue();
    }

    [Fact]
    public void PlaybackOptions_CanSetRepeatDelay()
    {
        // Arrange
        var options = new PlaybackOptions();

        // Act
        options.RepeatDelayMs = 1000;

        // Assert
        options.RepeatDelayMs.Should().Be(1000);
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(2.0)]
    [InlineData(10.0)]
    public void PlaybackOptions_AcceptsVariousSpeedMultipliers(double speed)
    {
        // Arrange
        var options = new PlaybackOptions();

        // Act
        options.SpeedMultiplier = speed;

        // Assert
        options.SpeedMultiplier.Should().Be(speed);
    }
}
