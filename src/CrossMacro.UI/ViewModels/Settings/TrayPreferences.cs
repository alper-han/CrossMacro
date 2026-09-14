namespace CrossMacro.UI.ViewModels.Settings;

/// <summary>The four desktop preferences that must be applied and persisted together.</summary>
internal readonly record struct TrayPreferences(
    bool EnableTrayIcon,
    bool StartMinimized,
    bool HideToTrayOnPlayback,
    bool HideToTrayOnRecording);
