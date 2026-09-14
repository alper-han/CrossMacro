using CrossMacro.Application.Runtime;
namespace CrossMacro.Infrastructure.Services.Runtime;

internal static class AutomationRuntimeSessionFactory
{
    internal static AutomationRuntimeSession Create(
        ISettingsService settings, HotkeySettings hotkeys, IGlobalHotkeyService? hotkey,
        IShortcutService? shortcut, ISchedulerService? scheduler, ITriggerService trigger,
        ITextExpansionService? textExpansion)
    {
        var components = new List<AutomationRuntimeComponent>();
        if (hotkey is not null)
        {
            components.Add(new("hotkey service", () => hotkey.IsRunning, _ =>
            {
                try
                {
                    hotkey.Start();
                    hotkey.ApplyHotkeys(hotkeys.RecordingHotkey, hotkeys.PlaybackHotkey, hotkeys.PauseHotkey);
                }
                catch (Exception error) when (error is not OutOfMemoryException)
                {
                    Log.Warning(error, "Hotkey service unavailable; desktop remains available for configuration");
                }
                return Task.CompletedTask;
            }, hotkey.StopHotkeyServiceAsync));
        }
        if (shortcut is not null)
        {
            components.Add(new("shortcut service", () => shortcut.IsListening,
                _ => { shortcut.Start(); return Task.CompletedTask; },
                _ => { shortcut.StopShortcuts(); return Task.CompletedTask; }));
        }
        components.Add(new("trigger service", () => trigger.IsMonitoring,
            _ => { trigger.Start(); return Task.CompletedTask; }, trigger.StopAsync, () => trigger.Completion.IsCompleted));
        if (scheduler is not null)
        {
            components.Add(new("scheduler", () => scheduler.IsRunning,
                _ => { scheduler.Start(); return Task.CompletedTask; }, scheduler.StopAsync, () => scheduler.Completion.IsCompleted));
        }
        if (textExpansion is not null)
        {
            components.Add(new("text expansion", () => textExpansion.IsRunning,
                textExpansion.StartAsync, textExpansion.StopExpansionAsync, IsEnabled: () => settings.Current.EnableTextExpansion));
        }
        return new AutomationRuntimeSession(components);
    }
}
