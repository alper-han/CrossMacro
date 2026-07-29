namespace CrossMacro.Core.Tests.Models;


public sealed class HotkeySettingsTests
{
    [Fact]
    public void NewHotkeySettings_HasCorrectDefaultHotkeys()
    {
        // Arrange & Act
        var settings = new HotkeySettings();

        // Assert
        _ = settings.RecordingHotkey.Should().Be(AppConstants.DefaultRecordingHotkey);
        _ = settings.PlaybackHotkey.Should().Be(AppConstants.DefaultPlaybackHotkey);
        _ = settings.PauseHotkey.Should().Be(AppConstants.DefaultPauseHotkey);
    }

    [Fact]
    public void HotkeySettings_DefaultRecordingHotkey_IsF8()
    {
        // Arrange & Act
        var settings = new HotkeySettings();

        // Assert
        _ = settings.RecordingHotkey.Should().Be("F8");
    }

    [Fact]
    public void HotkeySettings_DefaultPlaybackHotkey_IsF9()
    {
        // Arrange & Act
        var settings = new HotkeySettings();

        // Assert
        _ = settings.PlaybackHotkey.Should().Be("F9");
    }

    [Fact]
    public void HotkeySettings_DefaultPauseHotkey_IsF10()
    {
        // Arrange & Act
        var settings = new HotkeySettings();

        // Assert
        _ = settings.PauseHotkey.Should().Be("F10");
    }

    [Fact]
    public void HotkeySettings_CanSetCustomRecordingHotkey()
    {
        // Arrange
        var settings = new HotkeySettings
        {
            // Act
            RecordingHotkey = "Ctrl+Shift+R",
        };

        // Assert
        _ = settings.RecordingHotkey.Should().Be("Ctrl+Shift+R");
    }

    [Fact]
    public void HotkeySettings_CanSetCustomPlaybackHotkey()
    {
        // Arrange
        var settings = new HotkeySettings
        {
            // Act
            PlaybackHotkey = "Super+P",
        };

        // Assert
        _ = settings.PlaybackHotkey.Should().Be("Super+P");
    }

    [Theory]
    [InlineData("F1")]
    [InlineData("F12")]
    [InlineData("Ctrl+A")]
    [InlineData("Alt+Shift+X")]
    [InlineData("Super+J")]
    public void HotkeySettings_AcceptsVariousHotkeyFormats(string hotkey)
    {
        // Arrange
        var settings = new HotkeySettings
        {
            // Act
            RecordingHotkey = hotkey,
        };

        // Assert
        _ = settings.RecordingHotkey.Should().Be(hotkey);
    }
}
