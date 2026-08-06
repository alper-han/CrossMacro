namespace CrossMacro.Cli.DependencyInjection;

internal static class CliCommandHandlerRegistrationModule
{
    internal static void Register(IServiceCollection services)
    {
        _ = services.AddCliCommandHandler<MacroValidateCliOptions, MacroValidateCommandHandler>();
        _ = services.AddCliCommandHandler<MacroInfoCliOptions, MacroInfoCommandHandler>();
        _ = services.AddCliCommandHandler<PlayCliOptions, PlayCommandHandler>();
        _ = services.AddCliCommandHandler<DoctorCliOptions, DoctorCommandHandler>();
        _ = services.AddCliCommandHandler<SettingsGetCliOptions, SettingsGetCommandHandler>();
        _ = services.AddCliCommandHandler<SettingsSetCliOptions, SettingsSetCommandHandler>();
        _ = services.AddCliCommandHandler<SettingsListKeysCliOptions, SettingsListKeysCommandHandler>();
        _ = services.AddCliCommandHandler<SettingsResetCliOptions, SettingsResetCommandHandler>();
        _ = services.AddCliCommandHandler<ProfileCliOptions, ProfileCommandHandler>();
        _ = services.AddCliCommandHandler<TextExpansionCliOptions, TextExpansionCommandHandler>();
        _ = services.AddCliCommandHandler<ScheduleListCliOptions, ScheduleListCommandHandler>();
        _ = services.AddCliCommandHandler<ScheduleRunCliOptions, ScheduleRunCommandHandler>();
        _ = services.AddCliCommandHandler<ScheduleCliOptions, ScheduleCommandHandler>();
        _ = services.AddCliCommandHandler<ShortcutListCliOptions, ShortcutListCommandHandler>();
        _ = services.AddCliCommandHandler<ShortcutRunCliOptions, ShortcutRunCommandHandler>();
        _ = services.AddCliCommandHandler<ShortcutCliOptions, ShortcutCommandHandler>();
        _ = services.AddCliCommandHandler<TriggerListCliOptions, TriggerListCommandHandler>();
        _ = services.AddCliCommandHandler<TriggerCliOptions, TriggerCommandHandler>();
        _ = services.AddCliCommandHandler<RecordCliOptions, RecordCommandHandler>();
        _ = services.AddCliCommandHandler<RunCliOptions, RunCommandHandler>();
        _ = services.AddCliCommandHandler<InputCliOptions, InputCommandHandler>();
        _ = services.AddCliCommandHandler<ClipboardCliOptions, ClipboardCommandHandler>();
        _ = services.AddCliCommandHandler<WindowCliOptions, WindowCommandHandler>();
        _ = services.AddCliCommandHandler<ScreenCliOptions, ScreenCommandHandler>();
        _ = services.AddCliCommandHandler<ScreenshotCliOptions, ScreenshotCommandHandler>();
        _ = services.AddCliCommandHandler<HeadlessCliOptions, HeadlessCommandHandler>();
        _ = services.AddSingleton<ICliCommandHandlerResolver>(sp => new CliCommandHandlerResolver(sp.GetRequiredService<IEnumerable<CliCommandHandlerRegistration>>()));
        _ = services.AddSingleton<CliCommandExecutor>();
    }

    private static IServiceCollection AddCliCommandHandler<TOptions, [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(this IServiceCollection services)
        where TOptions : CliCommandOptions
        where THandler : class, ICliCommandHandler
    {
        _ = services.AddSingleton<THandler>();
        _ = services.AddSingleton<CliCommandHandlerRegistration>(sp => new CliCommandHandlerRegistration(typeof(TOptions), () => sp.GetRequiredService<THandler>()));
        return services;
    }
}
