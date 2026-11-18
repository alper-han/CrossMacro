namespace CrossMacro.Core.Tests.Models;

public sealed class HotkeySettingsTests
{
    [Fact]
    public void NewHotkeySettings_HasDistinctDefaultHotkeys()
    {
        var settings = new HotkeySettings();

        _ = settings.RecordingHotkey.Should().Be(AppConstants.DefaultRecordingHotkey);
        _ = settings.PlaybackHotkey.Should().Be(AppConstants.DefaultPlaybackHotkey);
        _ = settings.PauseHotkey.Should().Be(AppConstants.DefaultPauseHotkey);
        _ = new[] { settings.RecordingHotkey, settings.PlaybackHotkey, settings.PauseHotkey }.Should().OnlyHaveUniqueItems();
    }
}
