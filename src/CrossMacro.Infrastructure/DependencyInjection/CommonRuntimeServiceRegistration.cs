namespace CrossMacro.Infrastructure.DependencyInjection;

internal static class CommonRuntimeServiceRegistration
{
    internal static void Register(IServiceCollection services)
    {
        _ = services.AddSingleton<IHotkeyConfigurationService, HotkeyConfigurationService>();
        _ = services.AddSingleton<ISettingsService, SettingsService>();
        _ = services.AddSingleton<HotkeySettings>(sp => sp.GetRequiredService<IHotkeyConfigurationService>().Load());
        _ = services.AddSingleton<IRuntimeLogLevelService, RuntimeLogLevelService>();
        _ = services.AddSingleton(TimeProvider.System);
        services.TryAddSingleton<IShellCommandRunner>(sp => sp.GetRequiredService<IRuntimeContext>().IsFlatpak ? new FlatpakSandboxShellCommandRunner() : new ShellCommandRunner());
        _ = services.AddSingleton<Func<ICoordinateStrategy, IInputEventProcessor>>(_ => strategy => new StandardInputEventProcessor(strategy));
        _ = services.AddTransient<IMacroRecorder>(sp => new MacroRecorder(
            sp.GetService<Func<IInputCapture>>(), sp.GetRequiredService<ICoordinateStrategyFactory>(),
            sp.GetRequiredService<Func<ICoordinateStrategy, IInputEventProcessor>>(), sp.GetService<Func<IInputSimulator>>(),
            sp.GetService<IMousePositionProvider>(), sp.GetService<IInputSimulatorPool>()));
    }
}
