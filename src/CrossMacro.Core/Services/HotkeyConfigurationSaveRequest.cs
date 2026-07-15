namespace CrossMacro.Core.Services;

public sealed record class HotkeyConfigurationSaveRequest(
    string ConfigPath,
    string RecordingHotkey,
    string PlaybackHotkey,
    string PauseHotkey);
