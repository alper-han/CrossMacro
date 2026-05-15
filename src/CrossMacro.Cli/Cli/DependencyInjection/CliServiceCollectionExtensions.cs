using System;
using CrossMacro.Core.Services;
using CrossMacro.Cli.Commands;
using CrossMacro.Cli.Services;
using CrossMacro.Platform.Abstractions.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CrossMacro.Cli.DependencyInjection;

public static class CliServiceCollectionExtensions
{
    public static IServiceCollection AddCliServices(this IServiceCollection services)
    {
        // CLI runtime exposes simulator/capture as factories in platform registrars.
        // Materialize concrete instances for preflight/doctor services that depend on direct interfaces.
        services.AddTransient<IInputSimulator>(sp => sp.GetRequiredService<Func<IInputSimulator>>()());
        services.AddTransient<IInputCapture>(sp => sp.GetRequiredService<Func<IInputCapture>>()());

        services.AddSingleton<IMacroExecutionService, MacroExecutionService>();
        services.AddSingleton<IDoctorService>(sp =>
        {
            var linuxDaemonHandshakeProbe = sp.GetService<ILinuxDaemonHandshakeProbe>();
            Func<string, bool>? daemonHandshakeProbe = linuxDaemonHandshakeProbe is null
                ? null
                : linuxDaemonHandshakeProbe.Probe;
            Func<string, TimeSpan, LinuxDaemonHandshakeProbeResult>? daemonHandshakeDiagnosticProbe = linuxDaemonHandshakeProbe is null
                ? null
                : linuxDaemonHandshakeProbe.Probe;

            var linuxDaemonSocketAccessProbe = sp.GetService<ILinuxDaemonSocketAccessProbe>();
            Func<string, LinuxDaemonSocketAccessResult>? daemonSocketAccessProbe = linuxDaemonSocketAccessProbe is null
                ? null
                : socketPath => linuxDaemonSocketAccessProbe.Probe(new LinuxDaemonSocketProbeOptions(socketPath, "crossmacro"));

            return new DoctorService(
                sp.GetRequiredService<IEnvironmentInfoProvider>(),
                sp.GetRequiredService<IDisplaySessionService>(),
                sp.GetRequiredService<Func<IInputSimulator>>(),
                sp.GetRequiredService<Func<IInputCapture>>(),
                sp.GetRequiredService<IMousePositionProvider>(),
                sp.GetService<IPermissionChecker>(),
                daemonHandshakeProbe,
                daemonSocketAccessProbe,
                daemonHandshakeDiagnosticProbe);
        });
        services.AddSingleton<ICliPreflightService, CliPreflightService>();
        services.AddSingleton<ISettingsCliService, SettingsCliService>();
        services.AddSingleton<IScheduleCliService, ScheduleCliService>();
        services.AddSingleton<IShortcutCliService, ShortcutCliService>();
        services.AddSingleton<IRecordExecutionService, RecordExecutionService>();
        services.AddSingleton<IHeadlessHotkeyActionService, HeadlessHotkeyActionService>();
        services.AddSingleton<IHeadlessRuntimeService, HeadlessRuntimeService>();
        services.AddSingleton<IRunScriptExecutionService, RunScriptExecutionService>();

        services.AddSingleton<MacroValidateCommandHandler>();
        services.AddSingleton<MacroInfoCommandHandler>();
        services.AddSingleton<PlayCommandHandler>();
        services.AddSingleton<DoctorCommandHandler>();
        services.AddSingleton<SettingsGetCommandHandler>();
        services.AddSingleton<SettingsSetCommandHandler>();
        services.AddSingleton<ScheduleListCommandHandler>();
        services.AddSingleton<ScheduleRunCommandHandler>();
        services.AddSingleton<ShortcutListCommandHandler>();
        services.AddSingleton<ShortcutRunCommandHandler>();
        services.AddSingleton<RecordCommandHandler>();
        services.AddSingleton<RunCommandHandler>();
        services.AddSingleton<HeadlessCommandHandler>();

        services.AddSingleton<Func<MacroValidateCommandHandler>>(sp => sp.GetRequiredService<MacroValidateCommandHandler>);
        services.AddSingleton<Func<MacroInfoCommandHandler>>(sp => sp.GetRequiredService<MacroInfoCommandHandler>);
        services.AddSingleton<Func<PlayCommandHandler>>(sp => sp.GetRequiredService<PlayCommandHandler>);
        services.AddSingleton<Func<DoctorCommandHandler>>(sp => sp.GetRequiredService<DoctorCommandHandler>);
        services.AddSingleton<Func<SettingsGetCommandHandler>>(sp => sp.GetRequiredService<SettingsGetCommandHandler>);
        services.AddSingleton<Func<SettingsSetCommandHandler>>(sp => sp.GetRequiredService<SettingsSetCommandHandler>);
        services.AddSingleton<Func<ScheduleListCommandHandler>>(sp => sp.GetRequiredService<ScheduleListCommandHandler>);
        services.AddSingleton<Func<ScheduleRunCommandHandler>>(sp => sp.GetRequiredService<ScheduleRunCommandHandler>);
        services.AddSingleton<Func<ShortcutListCommandHandler>>(sp => sp.GetRequiredService<ShortcutListCommandHandler>);
        services.AddSingleton<Func<ShortcutRunCommandHandler>>(sp => sp.GetRequiredService<ShortcutRunCommandHandler>);
        services.AddSingleton<Func<RecordCommandHandler>>(sp => sp.GetRequiredService<RecordCommandHandler>);
        services.AddSingleton<Func<RunCommandHandler>>(sp => sp.GetRequiredService<RunCommandHandler>);
        services.AddSingleton<Func<HeadlessCommandHandler>>(sp => sp.GetRequiredService<HeadlessCommandHandler>);
        services.AddSingleton<ICliCommandHandlerResolver, CliCommandHandlerResolver>();
        services.AddSingleton<CliCommandExecutor>();
        return services;
    }
}
