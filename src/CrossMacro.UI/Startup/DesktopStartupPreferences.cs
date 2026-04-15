using System;
using CrossMacro.Core.Models;

namespace CrossMacro.UI.Startup;

internal enum DesktopStartupDisplayMode
{
    Visible,
    Minimized,
    HiddenToTray
}

internal readonly record struct DesktopStartupPreferences(
    bool ShouldStartMinimized,
    bool PersistTrayEnabled,
    bool UseStartupTrayOnly)
{
    public bool ShouldEnableTrayDuringStartup => PersistTrayEnabled || UseStartupTrayOnly;

    public DesktopStartupDisplayMode ResolveDisplayMode(bool trayAvailable)
    {
        if (!ShouldStartMinimized)
        {
            return DesktopStartupDisplayMode.Visible;
        }

        return ShouldEnableTrayDuringStartup && trayAvailable
            ? DesktopStartupDisplayMode.HiddenToTray
            : DesktopStartupDisplayMode.Minimized;
    }

    public static DesktopStartupPreferences Resolve(AppSettings settings, GuiStartupOptions startupOptions)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(startupOptions);

        var settingsStartMinimized = settings.StartMinimized;
        var settingsTrayEnabled = settings.EnableTrayIcon;
        var cliStartMinimized = startupOptions.StartMinimized;

        return new DesktopStartupPreferences(
            ShouldStartMinimized: settingsStartMinimized || cliStartMinimized,
            PersistTrayEnabled: settingsTrayEnabled || settingsStartMinimized,
            UseStartupTrayOnly: cliStartMinimized && !settingsTrayEnabled && !settingsStartMinimized);
    }
}
