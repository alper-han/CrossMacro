namespace CrossMacro.Cli.DependencyInjection;

internal static class CliPreflightServiceRegistration
{
    internal static void Register(IServiceCollection services)
    {
        _ = services.AddTransient<IInputCapture>(sp => sp.GetRequiredService<Func<IInputCapture>>()());
        _ = services.AddSingleton<IMacroExecutionService, MacroExecutionService>();
        _ = services.AddSingleton<IDoctorService>(sp => new DoctorService(
            sp.GetRequiredService<IRuntimeContext>(),
            sp.GetRequiredService<IDisplayEnvironmentDiagnostic>(),
            sp.GetRequiredService<IEnvironmentInfoProvider>(),
            sp.GetRequiredService<IDisplaySessionService>(),
            sp.GetRequiredService<Func<IInputSimulator>>(),
            sp.GetRequiredService<Func<IInputCapture>>(),
            sp.GetRequiredService<IMousePositionProvider>(),
            sp.GetRequiredService<ApplicationPaths>(),
            sp.GetService<IPermissionChecker>(),
            sp.GetService<ILinuxDaemonSocketAccessProbe>(),
            sp.GetService<ILinuxDaemonHandshakeProbe>(),
            sp.GetService<IScreenReadingDiagnosticProvider>(),
            sp.GetService<IMacOSScreenRecordingPermissionProbe>(),
            sp.GetService<IScreenReadingCapabilityReadiness>()));
        _ = services.AddSingleton<ICliPreflightService>(sp => new CliPreflightService(sp.GetRequiredService<IRuntimeContext>(), sp.GetRequiredService<IDisplaySessionService>(), sp.GetRequiredService<Func<IInputSimulator>>(), sp.GetRequiredService<Func<IInputCapture>>()));
    }
}
