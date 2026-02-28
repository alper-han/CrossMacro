using System;
using CrossMacro.Core.Services;
using CrossMacro.Cli.Commands;
using CrossMacro.Cli.Services;
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
        services.AddSingleton<IDoctorService, DoctorService>();
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

        services.AddSingleton<CliCommandExecutor>();
        return services;
    }
}
