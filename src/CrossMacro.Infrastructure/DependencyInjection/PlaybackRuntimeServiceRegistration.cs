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
        _ = services.AddTransient<IMacroPlayer>(sp =>
        {
            var dependencies = new MacroPlayerDependenciesFactory(sp, simulatorPoolResolver).Create();
            return new MacroPlayer(sp.GetRequiredService<IPlaybackValidator>(), dependencies);
        });
        _ = services.AddSingleton<Func<IMacroPlayer>>(sp => () => sp.GetRequiredService<IMacroPlayer>());
    }
}
