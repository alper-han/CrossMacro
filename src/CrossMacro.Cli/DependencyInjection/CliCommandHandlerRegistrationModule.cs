namespace CrossMacro.Cli.DependencyInjection;

internal static class CliCommandHandlerRegistrationModule
{
    internal static void Register(IServiceCollection services)
    {
        _ = services.AddCliCommandHandler<MacroValidateOptions, MacroValidateCommandHandler>();
        _ = services.AddCliCommandHandler<MacroInfoOptions, MacroInfoCommandHandler>();
        _ = services.AddCliCommandHandler<PlayOptions, PlayCommandHandler>();
        _ = services.AddCliCommandHandler<DoctorOptions, DoctorCommandHandler>();
        _ = services.AddCliCommandHandler<QuickSetupOptions, QuickSetupCommandHandler>();
        _ = services.AddCliCommandHandler<SettingsGetOptions, SettingsGetCommandHandler>();
        _ = services.AddCliCommandHandler<SettingsSetOptions, SettingsSetCommandHandler>();
        _ = services.AddCliCommandHandler<SettingsListKeysOptions, SettingsListKeysCommandHandler>();
        _ = services.AddCliCommandHandler<SettingsResetOptions, SettingsResetCommandHandler>();
        _ = services.AddCliCommandHandler<ProfileOptions, ProfileCommandHandler>();
        _ = services.AddCliCommandHandler<TextExpansionOptions, TextExpansionCommandHandler>();
        _ = services.AddCliCommandHandler<ScheduleListOptions>(static sp => TaskCommandHandlers.ScheduleList(sp.GetRequiredService<ITaskCommands<ScheduledTask, ScheduleCommand>>()));
        _ = services.AddCliCommandHandler<ScheduleRunOptions>(static sp => TaskCommandHandlers.ScheduleRun(sp.GetRequiredService<IRunnableTaskCommands<ScheduledTask, ScheduleCommand>>()));
        _ = services.AddCliCommandHandler<ScheduleOptions>(static sp => TaskCommandHandlers.Schedule(sp.GetRequiredService<ITaskCommands<ScheduledTask, ScheduleCommand>>()));
        _ = services.AddCliCommandHandler<ShortcutListOptions>(static sp => TaskCommandHandlers.ShortcutList(sp.GetRequiredService<ITaskCommands<ShortcutTask, ShortcutCommand>>()));
        _ = services.AddCliCommandHandler<ShortcutRunOptions>(static sp => TaskCommandHandlers.ShortcutRun(sp.GetRequiredService<IRunnableTaskCommands<ShortcutTask, ShortcutCommand>>()));
        _ = services.AddCliCommandHandler<ShortcutOptions>(static sp => TaskCommandHandlers.Shortcut(sp.GetRequiredService<ITaskCommands<ShortcutTask, ShortcutCommand>>()));
        _ = services.AddCliCommandHandler<TriggerListOptions>(static sp => TaskCommandHandlers.TriggerList(sp.GetRequiredService<ITaskCommands<TriggerTask, TriggerCommand>>()));
        _ = services.AddCliCommandHandler<TriggerOptions>(static sp => TaskCommandHandlers.Trigger(sp.GetRequiredService<ITaskCommands<TriggerTask, TriggerCommand>>()));
        _ = services.AddCliCommandHandler<RecordOptions, RecordCommandHandler>();
        _ = services.AddCliCommandHandler<RunOptions, RunCommandHandler>();
        _ = services.AddCliCommandHandler<InputOptions, InputCommandHandler>();
        _ = services.AddCliCommandHandler<ClipboardOptions, ClipboardCommandHandler>();
        _ = services.AddCliCommandHandler<WindowOptions, WindowCommandHandler>();
        _ = services.AddCliCommandHandler<ScreenOptions, ScreenCommandHandler>();
        _ = services.AddCliCommandHandler<ScreenshotOptions, ScreenshotCommandHandler>();
        _ = services.AddCliCommandHandler<HeadlessOptions, HeadlessCommandHandler>();
        _ = services.AddCliCommandHandler<McpOptions, McpCommandHandler>();
        _ = services.AddSingleton<ICliCommandHandlerResolver>(sp => new CliCommandHandlerResolver(sp.GetRequiredService<IEnumerable<CliCommandHandlerRegistration>>()));
        _ = services.AddSingleton<CliCommandExecutor>();
        _ = services.AddSingleton<ICommandLine, CliCommandLine>();
    }

    private static IServiceCollection AddCliCommandHandler<TOptions, [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(this IServiceCollection services)
        where TOptions : CommandOptions
        where THandler : class, ICliCommandHandler
    {
        _ = services.AddSingleton<THandler>();
        _ = services.AddSingleton<CliCommandHandlerRegistration>(sp => new CliCommandHandlerRegistration(typeof(TOptions), () => sp.GetRequiredService<THandler>()));
        return services;
    }

    private static IServiceCollection AddCliCommandHandler<TOptions>(this IServiceCollection services, Func<IServiceProvider, CliCommandHandlerBase<TOptions>> create)
        where TOptions : CommandOptions
    {
        _ = services.AddSingleton(create);
        _ = services.AddSingleton<CliCommandHandlerRegistration>(sp => new CliCommandHandlerRegistration(typeof(TOptions), () => sp.GetRequiredService<CliCommandHandlerBase<TOptions>>()));
        return services;
    }
}
