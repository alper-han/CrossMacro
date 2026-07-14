using System;
using CrossMacro.Application.Automation;
using CrossMacro.Application.Profiles;
using CrossMacro.Application.Runtime;
using CrossMacro.Core.Services;
using CrossMacro.Cli.Commands;
using CrossMacro.Cli.Services;
using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Abstractions.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CrossMacro.Cli.DependencyInjection;

public static class CliServiceCollectionExtensions
{
    public static IServiceCollection AddCliServices(this IServiceCollection services)
    {
        // CLI runtime exposes simulator/capture as factories in platform registrars.
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
                sp.GetService<IMacOSScreenRecordingPermissionProbe>());
        });
        services.AddSingleton<ICliPreflightService>(sp => new CliPreflightService(
            sp.GetRequiredService<IRuntimeContext>(),
            sp.GetRequiredService<IDisplaySessionService>(),
            sp.GetRequiredService<Func<IInputSimulator>>(),
            sp.GetRequiredService<Func<IInputCapture>>()));
        services.AddSingleton<ISettingsCliService, SettingsCliService>();
        services.AddSingleton<IManageProfile, ManageProfile>();
        services.AddSingleton<IManageTextExpansion, ManageTextExpansion>();
        services.AddSingleton<IManageShortcut, ManageShortcut>();
        services.AddSingleton<IManageSchedule, ManageSchedule>();
        services.AddSingleton<IManageTrigger, ManageTrigger>();
        services.AddSingleton<IProfileCliService, ProfileCliService>();
        services.AddSingleton<ITextExpansionCliService, TextExpansionCliService>();
        services.AddSingleton<IScheduleCliService>(sp =>
            new ScheduleCliService(sp.GetRequiredService<IManageSchedule>()));
        services.AddSingleton<IShortcutCliService>(sp =>
            new ShortcutCliService(sp.GetRequiredService<IManageShortcut>()));
        services.AddSingleton<ITriggerCliService>(sp =>
            new TriggerCliService(sp.GetRequiredService<IManageTrigger>()));
        services.AddSingleton<IRecordExecutionService, RecordExecutionService>();
        services.AddSingleton<IHeadlessHotkeyActionService, HeadlessHotkeyActionService>();
        services.AddSingleton<IRuntimeLifecycle>(sp => HeadlessRuntimeService.CreateLifecycle(
            sp.GetRequiredService<IGlobalHotkeyService>(),
            sp.GetRequiredService<ISchedulerService>(),
            sp.GetRequiredService<IShortcutService>(),
            sp.GetRequiredService<ITextExpansionService>(),
            sp.GetRequiredService<IHeadlessHotkeyActionService>(),
            sp.GetService<IScreenReadingWarmupService>()));
        services.AddSingleton<IHeadlessRuntimeService, HeadlessRuntimeService>();
        services.AddSingleton<IRunScriptExecutionService>(sp => new RunScriptExecutionService(
            sp.GetRequiredService<IRunExecutionService>()));
        services.AddSingleton<IClipboardCliService, ClipboardCliService>();
        services.AddSingleton<IWindowCliService, WindowCliService>();
        services.AddSingleton<IScreenCliService>(sp => new ScreenCliService(
            sp.GetService<IScreenPixelReader>(),
            sp.GetService<IMousePositionProvider>(),
            sp.GetService<IScreenImageAutomation>()));
        services.AddSingleton<IScreenshotCliService>(sp =>
            new ScreenshotCliService(sp.GetRequiredService<IScreenshotCaptureService>()));

        services.AddSingleton<MacroValidateCommandHandler>();
        services.AddSingleton<MacroInfoCommandHandler>();
        services.AddSingleton<PlayCommandHandler>();
        services.AddSingleton<DoctorCommandHandler>();
        services.AddSingleton<SettingsGetCommandHandler>();
        services.AddSingleton<SettingsSetCommandHandler>();
        services.AddSingleton<SettingsListKeysCommandHandler>();
        services.AddSingleton<SettingsResetCommandHandler>();
        services.AddSingleton<ProfileCommandHandler>();
        services.AddSingleton<TextExpansionCommandHandler>();
        services.AddSingleton<ScheduleListCommandHandler>();
        services.AddSingleton<ScheduleRunCommandHandler>();
        services.AddSingleton<ScheduleCommandHandler>();
        services.AddSingleton<ShortcutListCommandHandler>();
        services.AddSingleton<ShortcutRunCommandHandler>();
        services.AddSingleton<ShortcutCommandHandler>();
        services.AddSingleton<TriggerListCommandHandler>();
        services.AddSingleton<TriggerCommandHandler>();
        services.AddSingleton<RecordCommandHandler>();
        services.AddSingleton<RunCommandHandler>();
        services.AddSingleton<ClipboardCommandHandler>();
        services.AddSingleton<WindowCommandHandler>();
        services.AddSingleton<ScreenCommandHandler>();
        services.AddSingleton<ScreenshotCommandHandler>();
        services.AddSingleton<HeadlessCommandHandler>();

        services.AddSingleton<Func<MacroValidateCommandHandler>>(sp => sp.GetRequiredService<MacroValidateCommandHandler>);
        services.AddSingleton<Func<MacroInfoCommandHandler>>(sp => sp.GetRequiredService<MacroInfoCommandHandler>);
        services.AddSingleton<Func<PlayCommandHandler>>(sp => sp.GetRequiredService<PlayCommandHandler>);
        services.AddSingleton<Func<DoctorCommandHandler>>(sp => sp.GetRequiredService<DoctorCommandHandler>);
        services.AddSingleton<Func<SettingsGetCommandHandler>>(sp => sp.GetRequiredService<SettingsGetCommandHandler>);
        services.AddSingleton<Func<SettingsSetCommandHandler>>(sp => sp.GetRequiredService<SettingsSetCommandHandler>);
        services.AddSingleton<Func<SettingsListKeysCommandHandler>>(sp => sp.GetRequiredService<SettingsListKeysCommandHandler>);
        services.AddSingleton<Func<SettingsResetCommandHandler>>(sp => sp.GetRequiredService<SettingsResetCommandHandler>);
        services.AddSingleton<Func<ProfileCommandHandler>>(sp => sp.GetRequiredService<ProfileCommandHandler>);
        services.AddSingleton<Func<TextExpansionCommandHandler>>(sp => sp.GetRequiredService<TextExpansionCommandHandler>);
        services.AddSingleton<Func<ScheduleListCommandHandler>>(sp => sp.GetRequiredService<ScheduleListCommandHandler>);
        services.AddSingleton<Func<ScheduleRunCommandHandler>>(sp => sp.GetRequiredService<ScheduleRunCommandHandler>);
        services.AddSingleton<Func<ScheduleCommandHandler>>(sp => sp.GetRequiredService<ScheduleCommandHandler>);
        services.AddSingleton<Func<ShortcutListCommandHandler>>(sp => sp.GetRequiredService<ShortcutListCommandHandler>);
        services.AddSingleton<Func<ShortcutRunCommandHandler>>(sp => sp.GetRequiredService<ShortcutRunCommandHandler>);
        services.AddSingleton<Func<ShortcutCommandHandler>>(sp => sp.GetRequiredService<ShortcutCommandHandler>);
        services.AddSingleton<Func<TriggerListCommandHandler>>(sp => sp.GetRequiredService<TriggerListCommandHandler>);
        services.AddSingleton<Func<TriggerCommandHandler>>(sp => sp.GetRequiredService<TriggerCommandHandler>);
        services.AddSingleton<Func<RecordCommandHandler>>(sp => sp.GetRequiredService<RecordCommandHandler>);
        services.AddSingleton<Func<RunCommandHandler>>(sp => sp.GetRequiredService<RunCommandHandler>);
        services.AddSingleton<Func<ClipboardCommandHandler>>(sp => sp.GetRequiredService<ClipboardCommandHandler>);
        services.AddSingleton<Func<WindowCommandHandler>>(sp => sp.GetRequiredService<WindowCommandHandler>);
        services.AddSingleton<Func<ScreenCommandHandler>>(sp => sp.GetRequiredService<ScreenCommandHandler>);
        services.AddSingleton<Func<ScreenshotCommandHandler>>(sp => sp.GetRequiredService<ScreenshotCommandHandler>);
        services.AddSingleton<Func<HeadlessCommandHandler>>(sp => sp.GetRequiredService<HeadlessCommandHandler>);
        services.AddSingleton<ICliCommandHandlerResolver, CliCommandHandlerResolver>();
        services.AddSingleton<CliCommandExecutor>();
        return services;
    }
}
