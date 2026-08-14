namespace CrossMacro.Cli.DependencyInjection;

internal static class CliPreflightServiceRegistration
{
    internal static void Register(IServiceCollection services)
    {
        _ = services.AddTransient<IInputCapture>(sp => sp.GetRequiredService<Func<IInputCapture>>()());
        _ = services.AddSingleton<IMacroExecutionService, MacroExecutionService>();
        _ = services.AddSingleton<IDoctorService>(sp =>
        {
            var linuxDaemonHandshakeProbe = sp.GetService<ILinuxDaemonHandshakeProbe>();
            Func<string, bool>? daemonHandshakeProbe = linuxDaemonHandshakeProbe is null ? null : linuxDaemonHandshakeProbe.Probe;
            Func<string, TimeSpan, LinuxDaemonHandshakeProbeResult>? daemonHandshakeDiagnosticProbe = linuxDaemonHandshakeProbe is null ? null : linuxDaemonHandshakeProbe.Probe;
            var linuxDaemonSocketAccessProbe = sp.GetService<ILinuxDaemonSocketAccessProbe>();
            Func<string, CancellationToken, ValueTask<LinuxDaemonSocketAccessResult>>? daemonSocketAccessProbe = linuxDaemonSocketAccessProbe is null ? null : (socketPath, cancellationToken) => linuxDaemonSocketAccessProbe.ProbeAsync(new LinuxDaemonSocketProbeOptions(socketPath, "crossmacro"), cancellationToken);
            var linuxDaemonDiagnosticsEnabled = linuxDaemonHandshakeProbe is not null && linuxDaemonSocketAccessProbe is not null;
            return new DoctorService(
                sp.GetRequiredService<IRuntimeContext>(),
                sp.GetRequiredService<IDisplayEnvironmentDiagnostic>(),
                sp.GetRequiredService<IEnvironmentInfoProvider>(),
                sp.GetRequiredService<IDisplaySessionService>(),
                sp.GetRequiredService<Func<IInputSimulator>>(),
                sp.GetRequiredService<Func<IInputCapture>>(),
                sp.GetRequiredService<IMousePositionProvider>(),
                sp.GetService<IPermissionChecker>(),
                daemonHandshakeProbe,
                daemonSocketAccessProbe,
                daemonHandshakeDiagnosticProbe,
                sp.GetService<IScreenReadingDiagnosticProvider>(),
                sp.GetService<IMacOSScreenRecordingPermissionProbe>(),
                screenReadingCapabilityReadiness: sp.GetService<IScreenReadingCapabilityReadiness>(),
                linuxDaemonDiagnosticsEnabled: linuxDaemonDiagnosticsEnabled);
        });
        _ = services.AddSingleton<ICliPreflightService>(sp => new CliPreflightService(sp.GetRequiredService<IRuntimeContext>(), sp.GetRequiredService<IDisplaySessionService>(), sp.GetRequiredService<Func<IInputSimulator>>(), sp.GetRequiredService<Func<IInputCapture>>()));
    }
}
