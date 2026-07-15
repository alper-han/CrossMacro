namespace CrossMacro.Core.Services;

public sealed record HotkeyConfigurationSaveRequest(
    string ConfigPath,
    string RecordingHotkey,
    string PlaybackHotkey,
    string PauseHotkey);
