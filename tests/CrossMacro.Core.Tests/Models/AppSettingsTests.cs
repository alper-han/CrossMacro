namespace CrossMacro.Core.Tests.Models;


public sealed class AppSettingsTests
{
    [Fact]
    public void NewAppSettings_HasCorrectDefaultValues()
    {
        var settings = new AppSettings();

        _ = settings.EnableTrayIcon.Should().BeFalse();
        _ = settings.StartMinimized.Should().BeFalse();
        _ = settings.SuppressFastLoopWarning.Should().BeFalse();

        _ = settings.PlaybackSpeed.Should().Be(1.0);
        _ = settings.IsLooping.Should().BeFalse();
        _ = settings.LoopCount.Should().Be(1);
        _ = settings.LoopDelayMs.Should().Be(0);
        _ = settings.UseRandomLoopDelay.Should().BeFalse();
        _ = settings.LoopDelayMinMs.Should().Be(0);
        _ = settings.LoopDelayMaxMs.Should().Be(0);
        _ = settings.CountdownSeconds.Should().Be(0);

        _ = settings.IsMouseRecordingEnabled.Should().BeTrue();
        _ = settings.IsKeyboardRecordingEnabled.Should().BeTrue();
        _ = settings.ForceRelativeCoordinates.Should().BeFalse();
        _ = settings.SkipInitialZeroZero.Should().BeFalse();

        _ = settings.EnableTextExpansion.Should().BeFalse();
        _ = settings.CheckForUpdates.Should().BeFalse();
    }

    [Fact]
    public void AppSettings_CanSetPlaybackSpeed()
    {
        var settings = new AppSettings
        {
            PlaybackSpeed = 2.5,
        };

        _ = settings.PlaybackSpeed.Should().Be(2.5);
    }

    [Fact]
    public void AppSettings_CanSetLoopingOptions()
    {
        var settings = new AppSettings
        {
            IsLooping = true,
            LoopCount = 10,
            LoopDelayMs = 500,
            UseRandomLoopDelay = true,
            LoopDelayMinMs = 200,
            LoopDelayMaxMs = 800,
        };

        _ = settings.IsLooping.Should().BeTrue();
        _ = settings.LoopCount.Should().Be(10);
        _ = settings.LoopDelayMs.Should().Be(500);
        _ = settings.UseRandomLoopDelay.Should().BeTrue();
        _ = settings.LoopDelayMinMs.Should().Be(200);
        _ = settings.LoopDelayMaxMs.Should().Be(800);
    }

    [Fact]
    public void AppSettings_LoopDelayMax_ClampsToMin()
    {
        var settings = new AppSettings
        {
            LoopDelayMinMs = 300,
            LoopDelayMaxMs = 100,
        };
        settings.Normalize();

        _ = settings.LoopDelayMinMs.Should().Be(300);
        _ = settings.LoopDelayMaxMs.Should().Be(300);
    }

    [Fact]
    public void AppSettings_CanSetRecordingOptions()
    {
        var settings = new AppSettings
        {
            IsMouseRecordingEnabled = false,
            IsKeyboardRecordingEnabled = false,
        };

        _ = settings.IsMouseRecordingEnabled.Should().BeFalse();
        _ = settings.IsKeyboardRecordingEnabled.Should().BeFalse();
    }

    [Fact]
    public void AppSettings_CanEnableTextExpansion()
    {
        var settings = new AppSettings
        {
            EnableTextExpansion = true,
        };

        _ = settings.EnableTextExpansion.Should().BeTrue();
    }

    [Fact]
    public void AppSettings_CanEnableStartMinimized()
    {
        var settings = new AppSettings
        {
            StartMinimized = true,
        };

        _ = settings.StartMinimized.Should().BeTrue();
    }

    [Fact]
    public void AppSettings_CanSetCountdownSeconds()
    {
        var settings = new AppSettings
        {
            CountdownSeconds = 5,
        };

        _ = settings.CountdownSeconds.Should().Be(5);
    }
}
