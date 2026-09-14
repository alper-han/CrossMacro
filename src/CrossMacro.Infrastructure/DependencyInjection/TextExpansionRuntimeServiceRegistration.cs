namespace CrossMacro.Infrastructure.DependencyInjection;

internal static class TextExpansionRuntimeServiceRegistration
{
    internal static void Register(IServiceCollection services)
    {
        _ = services.AddSingleton<ITextExpansionStorageService, TextExpansionStorageService>();
        _ = services.AddSingleton<ITextExpansionStore>(sp => sp.GetRequiredService<ITextExpansionStorageService>());
        _ = services.AddSingleton<IProfileTextExpansionStore, ProfileTextExpansionStore>();
        _ = services.AddSingleton<IProfileLoadedMacroSessionStore, ProfileLoadedMacroSessionStore>();
        _ = services.AddSingleton<IInputProcessor>(sp => new InputProcessor(
            sp.GetRequiredService<IKeyboardLayoutService>(),
            sp.GetRequiredService<TimeProvider>()));
        _ = services.AddSingleton<ITextBufferState, TextBufferState>();
        _ = services.AddSingleton<ITextExpansionExecutor>(sp => new TextExpansionExecutor(
            sp.GetRequiredService<IClipboardService>(),
            sp.GetRequiredService<IKeyboardLayoutService>(),
            sp.GetRequiredService<Func<IInputSimulator>>(),
            sp.GetRequiredService<TimeProvider>()));
        _ = services.AddSingleton<ITextExpansionService, TextExpansionService>();
    }
}
