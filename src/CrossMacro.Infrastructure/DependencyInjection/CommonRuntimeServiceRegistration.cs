namespace CrossMacro.Infrastructure.DependencyInjection;

internal static class CommonRuntimeServiceRegistration
{
    internal static void Register(IServiceCollection services)
    {
        services.TryAddSingleton(_ => ApplicationPathsEnvironment.CaptureCurrent());
        _ = services.AddSingleton<IHotkeyConfigurationService>(sp => new HotkeyConfigurationService(sp.GetRequiredService<ApplicationPaths>().ConfigDirectory));
        _ = services.AddSingleton<ISettingsService>(sp => new SettingsService(sp.GetRequiredService<ApplicationPaths>().ConfigDirectory));
        _ = services.AddSingleton<HotkeySettings>(sp => sp.GetRequiredService<IHotkeyConfigurationService>().Load());
        services.TryAddSingleton<IRuntimeLogLevelService, RuntimeLogLevelService>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IShellCommandRunner>(sp => sp.GetRequiredService<IRuntimeContext>().IsFlatpak ? new FlatpakSandboxShellCommandRunner() : new ShellCommandRunner());
        _ = services.AddSingleton<Func<ICoordinateStrategy, IInputEventProcessor>>(_ => strategy => new StandardInputEventProcessor(strategy));
        _ = services.AddTransient<IMacroRecorder>(sp => new MacroRecorder(
            sp.GetService<Func<IInputCapture>>(), sp.GetRequiredService<ICoordinateStrategyFactory>(),
            sp.GetRequiredService<Func<ICoordinateStrategy, IInputEventProcessor>>(), sp.GetService<Func<IInputSimulator>>(),
            sp.GetService<IMousePositionProvider>(), sp.GetService<IInputSimulatorPool>()));
    }
}
