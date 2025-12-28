namespace CrossMacro.Core.Tests.Models;

using CrossMacro.Core.Models;
using FluentAssertions;

public class AppSettingsTests
{
    [Fact]
    public void NewAppSettings_HasCorrectDefaultValues()
    {
        // Arrange & Act
        var settings = new AppSettings();

        // Assert - Tray Settings
        settings.EnableTrayIcon.Should().BeTrue();

        // Assert - Playback Settings
        settings.PlaybackSpeed.Should().Be(1.0);
        settings.IsLooping.Should().BeFalse();
        settings.LoopCount.Should().Be(1);
        settings.LoopDelayMs.Should().Be(0);
        settings.CountdownSeconds.Should().Be(0);

        // Assert - Recording Settings
        settings.IsMouseRecordingEnabled.Should().BeTrue();
        settings.IsKeyboardRecordingEnabled.Should().BeTrue();
        settings.ForceRelativeCoordinates.Should().BeFalse();
        settings.SkipInitialZeroZero.Should().BeFalse();

        // Assert - Text Expansion Settings
        settings.EnableTextExpansion.Should().BeFalse();

        // Assert - Update Settings
        settings.CheckForUpdates.Should().BeFalse();
    }

    [Fact]
    public void AppSettings_CanSetPlaybackSpeed()
    {
        // Arrange
        var settings = new AppSettings();

        // Act
        settings.PlaybackSpeed = 2.5;

        // Assert
        settings.PlaybackSpeed.Should().Be(2.5);
    }

    [Fact]
    public void AppSettings_CanSetLoopingOptions()
    {
        // Arrange
        var settings = new AppSettings();

        // Act
        settings.IsLooping = true;
        settings.LoopCount = 10;
        settings.LoopDelayMs = 500;

        // Assert
        settings.IsLooping.Should().BeTrue();
        settings.LoopCount.Should().Be(10);
        settings.LoopDelayMs.Should().Be(500);
    }

    [Fact]
    public void AppSettings_CanSetRecordingOptions()
    {
        // Arrange
        var settings = new AppSettings();

        // Act
        settings.IsMouseRecordingEnabled = false;
        settings.IsKeyboardRecordingEnabled = false;

        // Assert
        settings.IsMouseRecordingEnabled.Should().BeFalse();
        settings.IsKeyboardRecordingEnabled.Should().BeFalse();
    }

    [Fact]
    public void AppSettings_CanEnableTextExpansion()
    {
        // Arrange
        var settings = new AppSettings();

        // Act
        settings.EnableTextExpansion = true;

        // Assert
        settings.EnableTextExpansion.Should().BeTrue();
    }

    [Fact]
    public void AppSettings_CanSetCountdownSeconds()
    {
        // Arrange
        var settings = new AppSettings();

        // Act
        settings.CountdownSeconds = 5;

        // Assert
        settings.CountdownSeconds.Should().Be(5);
    }
}
