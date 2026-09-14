namespace CrossMacro.Core.Services.Settings;

public sealed record HotkeyConfigurationSaveRequest(
    string ConfigPath,
    string RecordingHotkey,
    string PlaybackHotkey,
    string PauseHotkey);
