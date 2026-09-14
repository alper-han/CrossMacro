namespace CrossMacro.Infrastructure.DependencyInjection;

internal static class PlaybackRuntimeServiceRegistration
{
    internal static void Register(IServiceCollection services, Func<IServiceProvider, IInputSimulatorPool?> simulatorPoolResolver)
    {
        _ = services.AddSingleton<IGlobalHotkeyService>(sp => new GlobalHotkeyService(
            sp.GetRequiredService<IHotkeyConfigurationService>(),
            sp.GetRequiredService<IHotkeyParser>(),
            sp.GetRequiredService<IHotkeyMatcher>(),
            sp.GetRequiredService<IModifierStateTracker>(),
            sp.GetRequiredService<IHotkeyStringBuilder>(),
            sp.GetRequiredService<IMouseButtonMapper>(),
            sp.GetService<Func<IInputCapture>>(),
            sp.GetRequiredService<HotkeySettings>()));
        _ = services.AddSingleton<IImageClickMovementResolver>(sp => new ImageClickMovementResolver(sp.GetRequiredService<IMousePositionProvider>()));
        _ = services.AddTransient<IPlaybackValidator, PlaybackValidator>();
        _ = services.AddTransient<IMacroPlayer>(sp => CreatePlayer(sp, simulatorPoolResolver));
        // Factory callers own and dispose each playback session. Resolving a disposable
        // transient from the root provider would retain every session until host shutdown.
        _ = services.AddSingleton<Func<IMacroPlayer>>(sp => () => CreatePlayer(sp, simulatorPoolResolver));
    }

    private static IMacroPlayer CreatePlayer(
        IServiceProvider services,
        Func<IServiceProvider, IInputSimulatorPool?> simulatorPoolResolver) =>
        new MacroPlayer(
            services.GetRequiredService<IPlaybackValidator>(),
            MacroPlayerDependenciesFactory.Create(services, simulatorPoolResolver));
}
