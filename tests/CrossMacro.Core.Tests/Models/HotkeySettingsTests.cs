namespace CrossMacro.Core.Tests.Models;

using CrossMacro.Core;
using CrossMacro.Core.Models;
using FluentAssertions;

public class HotkeySettingsTests
{
    [Fact]
    public void NewHotkeySettings_HasCorrectDefaultHotkeys()
    {
        // Arrange & Act
        var settings = new HotkeySettings();

        // Assert
        settings.RecordingHotkey.Should().Be(AppConstants.DefaultRecordingHotkey);
        settings.PlaybackHotkey.Should().Be(AppConstants.DefaultPlaybackHotkey);
        settings.PauseHotkey.Should().Be(AppConstants.DefaultPauseHotkey);
    }

    [Fact]
    public void HotkeySettings_DefaultRecordingHotkey_IsF8()
    {
        // Arrange & Act
        var settings = new HotkeySettings();

        // Assert
        settings.RecordingHotkey.Should().Be("F8");
    }

    [Fact]
    public void HotkeySettings_DefaultPlaybackHotkey_IsF9()
    {
        // Arrange & Act
        var settings = new HotkeySettings();

        // Assert
        settings.PlaybackHotkey.Should().Be("F9");
    }

    [Fact]
    public void HotkeySettings_DefaultPauseHotkey_IsF10()
    {
        // Arrange & Act
        var settings = new HotkeySettings();

        // Assert
        settings.PauseHotkey.Should().Be("F10");
    }

    [Fact]
    public void HotkeySettings_CanSetCustomRecordingHotkey()
    {
        // Arrange
        var settings = new HotkeySettings();

        // Act
        settings.RecordingHotkey = "Ctrl+Shift+R";

        // Assert
        settings.RecordingHotkey.Should().Be("Ctrl+Shift+R");
    }

    [Fact]
    public void HotkeySettings_CanSetCustomPlaybackHotkey()
    {
        // Arrange
        var settings = new HotkeySettings();

        // Act
        settings.PlaybackHotkey = "Super+P";

        // Assert
        settings.PlaybackHotkey.Should().Be("Super+P");
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
        var settings = new HotkeySettings();

        // Act
        settings.RecordingHotkey = hotkey;

        // Assert
        settings.RecordingHotkey.Should().Be(hotkey);
    }
}

