namespace CrossMacro.Core.Tests.Models;

public sealed class AppSettingsTests
{
    [Fact]
    public void NewAppSettings_HasCorrectDefaultValues()
    {
        var settings = new AppSettings();

        _ = settings.McpSecurity.Should().NotBeNull();
        _ = settings.EnableTrayIcon.Should().BeFalse();
        _ = settings.StartMinimized.Should().BeFalse();
        _ = settings.HideToTrayOnPlayback.Should().BeFalse();
        _ = settings.HideToTrayOnRecording.Should().BeFalse();
        _ = settings.SuppressFastLoopWarning.Should().BeFalse();
        _ = settings.MacOSScreenRecordingOnboardingCompleted.Should().BeFalse();

        _ = settings.PlaybackSpeed.Should().Be(1.0);
        _ = settings.IsLooping.Should().BeFalse();
        _ = settings.LoopCount.Should().Be(1);
        _ = settings.LoopDelayMs.Should().Be(0);
        _ = settings.UseRandomLoopDelay.Should().BeFalse();
        _ = settings.LoopDelayMinMs.Should().Be(0);
        _ = settings.LoopDelayMaxMs.Should().Be(0);
        _ = settings.MotionMode.Should().Be(MotionPlaybackMode.Precision);
        _ = settings.StrictSpeedMotionEventsPerSecond.Should().Be(1_000);
        _ = settings.PrecisionMotionEventsPerSecond.Should().Be(300);
        _ = settings.MaximumMotionErrorPixels.Should().Be(2d);
        _ = settings.CountdownSeconds.Should().Be(0);

        _ = settings.IsMouseRecordingEnabled.Should().BeTrue();
        _ = settings.IsKeyboardRecordingEnabled.Should().BeTrue();
        _ = settings.ForceRelativeCoordinates.Should().BeFalse();
        _ = settings.UseLogicalRelativeCoordinates.Should().BeFalse();
        _ = settings.SkipInitialZeroZero.Should().BeFalse();

        _ = settings.EnableTextExpansion.Should().BeFalse();
        _ = settings.CheckForUpdates.Should().BeFalse();
        _ = settings.Theme.Should().Be("Mocha");
        _ = settings.ThemeMode.Should().Be(ThemePreferenceMode.System);
        _ = settings.Language.Should().Be("en");
    }

    [Fact]
    public void Normalize_LoopDelayMaxBelowMin_ClampsMaxToMin()
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
    public void Normalize_OutOfRangePlaybackValues_AreBroughtIntoSupportedRange()
    {
        var settings = new AppSettings
        {
            PlaybackSpeed = double.NaN,
            LoopDelayMs = -5,
            MotionMode = (MotionPlaybackMode)999,
            StrictSpeedMotionEventsPerSecond = int.MaxValue,
            PrecisionMotionEventsPerSecond = 0,
            MaximumMotionErrorPixels = double.PositiveInfinity,
        };

        settings.Normalize();

        _ = settings.PlaybackSpeed.Should().Be(PlaybackOptions.DefaultSpeedMultiplier);
        _ = settings.LoopDelayMs.Should().Be(0);
        _ = settings.MotionMode.Should().Be(MotionPlaybackMode.Precision);
        _ = settings.StrictSpeedMotionEventsPerSecond.Should().Be(PlaybackOptions.MaxStrictSpeedMotionEventsPerSecond);
        _ = settings.PrecisionMotionEventsPerSecond.Should().Be(PlaybackOptions.DefaultPrecisionMotionEventsPerSecond);
        _ = settings.MaximumMotionErrorPixels.Should().Be(PlaybackOptions.DefaultMaximumMotionErrorPixels);
    }

    [Fact]
    public void Normalize_WhenMcpSecurityWasDeserializedAsNull_RestoresDefaults()
    {
        var settings = new AppSettings { McpSecurity = null! };

        settings.Normalize();

        _ = settings.McpSecurity.Should().NotBeNull();
    }
}
