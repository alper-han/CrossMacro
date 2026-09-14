namespace CrossMacro.UI.ViewModels.Settings;

internal static class TrayPreferencePolicy
{
    internal static TrayPreferences Apply(TrayPreferences before, TrayPreference preference, bool value, bool traySupported)
    {
        var after = preference switch
        {
            TrayPreference.EnableTrayIcon => before with { EnableTrayIcon = value },
            TrayPreference.StartMinimized => before with { StartMinimized = value },
            TrayPreference.HideToTrayOnPlayback => before with { HideToTrayOnPlayback = value },
            TrayPreference.HideToTrayOnRecording => before with { HideToTrayOnRecording = value },
            _ => throw new ArgumentOutOfRangeException(nameof(preference), preference, message: null),
        };
        if (after == before || !traySupported) { return after; }
        if (preference is TrayPreference.EnableTrayIcon)
        {
            return value ? after : after with
            {
                StartMinimized = false,
                HideToTrayOnPlayback = false,
                HideToTrayOnRecording = false,
            };
        }
        return value ? after with { EnableTrayIcon = true } : after;
    }
}
