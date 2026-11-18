namespace CrossMacro.Infrastructure.DependencyInjection;

internal static class HotkeyRuntimeServiceRegistration
{
    internal static void Register(IServiceCollection services)
    {
        _ = services.AddSingleton<ModifierStateTracker>();
        _ = services.AddSingleton<HotkeyParser>();
        _ = services.AddSingleton<HotkeyStringBuilder>();
        _ = services.AddSingleton<HotkeyMatcher>();
        _ = services.AddSingleton<IShortcutHotkeyNormalizer, ShortcutHotkeyNormalizer>();
        _ = services.AddSingleton<IGlobalHotkeyService, GlobalHotkeyService>();
    }
}
