namespace CrossMacro.Core.Tests.Models;

using CrossMacro.Core.Models;
using FluentAssertions;

public class AppSettingsTests
{
    [Fact]
    public void NewAppSettings_HasCorrectDefaultValues()
    {
        var settings = new AppSettings();

        settings.EnableTrayIcon.Should().BeFalse();
        settings.StartMinimized.Should().BeFalse();

        settings.PlaybackSpeed.Should().Be(1.0);
        settings.IsLooping.Should().BeFalse();
        settings.LoopCount.Should().Be(1);
        settings.LoopDelayMs.Should().Be(0);
        settings.UseRandomLoopDelay.Should().BeFalse();
        settings.LoopDelayMinMs.Should().Be(0);
        settings.LoopDelayMaxMs.Should().Be(0);
        settings.CountdownSeconds.Should().Be(0);

        settings.IsMouseRecordingEnabled.Should().BeTrue();
        settings.IsKeyboardRecordingEnabled.Should().BeTrue();
        settings.ForceRelativeCoordinates.Should().BeFalse();
        settings.SkipInitialZeroZero.Should().BeFalse();

        settings.EnableTextExpansion.Should().BeFalse();
        settings.CheckForUpdates.Should().BeFalse();
    }

    [Fact]
    public void AppSettings_CanSetPlaybackSpeed()
    {
        var settings = new AppSettings();

        settings.PlaybackSpeed = 2.5;

        settings.PlaybackSpeed.Should().Be(2.5);
    }

    [Fact]
    public void AppSettings_CanSetLoopingOptions()
    {
        var settings = new AppSettings();

        settings.IsLooping = true;
        settings.LoopCount = 10;
        settings.LoopDelayMs = 500;
        settings.UseRandomLoopDelay = true;
        settings.LoopDelayMinMs = 200;
        settings.LoopDelayMaxMs = 800;

        settings.IsLooping.Should().BeTrue();
        settings.LoopCount.Should().Be(10);
        settings.LoopDelayMs.Should().Be(500);
        settings.UseRandomLoopDelay.Should().BeTrue();
        settings.LoopDelayMinMs.Should().Be(200);
        settings.LoopDelayMaxMs.Should().Be(800);
    }

    [Fact]
    public void AppSettings_LoopDelayMax_ClampsToMin()
    {
        var settings = new AppSettings();

        settings.LoopDelayMinMs = 300;
        settings.LoopDelayMaxMs = 100;

        settings.LoopDelayMinMs.Should().Be(300);
        settings.LoopDelayMaxMs.Should().Be(300);
    }

    [Fact]
    public void AppSettings_CanSetRecordingOptions()
    {
        var settings = new AppSettings();

        settings.IsMouseRecordingEnabled = false;
        settings.IsKeyboardRecordingEnabled = false;

        settings.IsMouseRecordingEnabled.Should().BeFalse();
        settings.IsKeyboardRecordingEnabled.Should().BeFalse();
    }

    [Fact]
    public void AppSettings_CanEnableTextExpansion()
    {
        var settings = new AppSettings();

        settings.EnableTextExpansion = true;

        settings.EnableTextExpansion.Should().BeTrue();
    }

    [Fact]
    public void AppSettings_CanEnableStartMinimized()
    {
        var settings = new AppSettings
        {
            StartMinimized = true
        };

        settings.StartMinimized.Should().BeTrue();
    }

    [Fact]
    public void AppSettings_CanSetCountdownSeconds()
    {
        var settings = new AppSettings();

        settings.CountdownSeconds = 5;

        settings.CountdownSeconds.Should().Be(5);
    }
}
