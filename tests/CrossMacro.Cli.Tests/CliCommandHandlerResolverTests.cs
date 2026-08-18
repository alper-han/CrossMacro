
namespace CrossMacro.Cli.Tests;

public sealed class CliCommandHandlerResolverTests
{
    [Theory]
    [MemberData(nameof(KnownCommandOptions))]
    public void Resolve_WhenKnownOptions_ReturnsExpectedHandler(CliCommandOptions options, Type expectedHandlerType)
    {
        var handlers = CreateHandlers();
        var resolver = CreateResolver(handlers);

        var handler = resolver.Resolve(options);

        Assert.NotNull(handler);
        Assert.IsType(expectedHandlerType, handler);
    }

    [Fact]
    public void Resolve_WhenUnknownOptions_ReturnsNull()
    {
        var resolver = CreateResolver(CreateHandlers());

        var handler = resolver.Resolve(new UnknownCliOptions());

        Assert.Null(handler);
    }

    [Fact]
    public void Resolve_WhenKnownOptions_OnlyCreatesRequestedHandler()
    {
        var requestedFactoryCalls = 0;
        var unrelatedFactoryCalls = 0;
        var requestedHandler = Substitute.For<ICliCommandHandler>();
        var resolver = new CliCommandHandlerResolver(
        [
            new CliCommandHandlerRegistration(
                typeof(MacroValidateCliOptions),
                () =>
                {
                    requestedFactoryCalls++;
                    return requestedHandler;
                }),
            new CliCommandHandlerRegistration(
                typeof(MacroInfoCliOptions),
                () =>
                {
                    unrelatedFactoryCalls++;
                    return Substitute.For<ICliCommandHandler>();
                }),
        ]);

        var handler = resolver.Resolve(new MacroValidateCliOptions("demo.macro"));

        Assert.Same(requestedHandler, handler);
        Assert.Equal(1, requestedFactoryCalls);
        Assert.Equal(0, unrelatedFactoryCalls);
    }

    [Fact]
    public void Create_WhenMultipleHandlersAreRegisteredForOptionsType_ThrowsClearly()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new CliCommandHandlerResolver(
        [
            new CliCommandHandlerRegistration(typeof(MacroValidateCliOptions), () => Substitute.For<ICliCommandHandler>()),
            new CliCommandHandlerRegistration(typeof(MacroValidateCliOptions), () => Substitute.For<ICliCommandHandler>()),
        ]));

        Assert.Contains(nameof(MacroValidateCliOptions), exception.Message, StringComparison.Ordinal);
    }

    public static IEnumerable<object[]> KnownCommandOptions()
    {
        yield return [new MacroValidateCliOptions("demo.macro"), typeof(MacroValidateCommandHandler)];
        yield return [new MacroInfoCliOptions("demo.macro"), typeof(MacroInfoCommandHandler)];
        yield return [new PlayCliOptions("demo.macro"), typeof(PlayCommandHandler)];
        yield return [new DoctorCliOptions(), typeof(DoctorCommandHandler)];
        yield return [new QuickSetupCliOptions(), typeof(QuickSetupCommandHandler)];
        yield return [new SettingsGetCliOptions(), typeof(SettingsGetCommandHandler)];
        yield return [new SettingsSetCliOptions("playback.speed", "1.25"), typeof(SettingsSetCommandHandler)];
        yield return [new SettingsListKeysCliOptions(), typeof(SettingsListKeysCommandHandler)];
        yield return [new SettingsResetCliOptions("ui.theme"), typeof(SettingsResetCommandHandler)];
        yield return [new ProfileCliOptions(ProfileCliAction.List), typeof(ProfileCommandHandler)];
        yield return [new TextExpansionCliOptions(TextExpansionCliAction.List), typeof(TextExpansionCommandHandler)];
        yield return [new ScheduleListCliOptions(), typeof(ScheduleListCommandHandler)];
        yield return [new ScheduleRunCliOptions("task-id"), typeof(ScheduleRunCommandHandler)];
        yield return [new ScheduleCliOptions(ScheduleCliAction.Add), typeof(ScheduleCommandHandler)];
        yield return [new ShortcutListCliOptions(), typeof(ShortcutListCommandHandler)];
        yield return [new ShortcutRunCliOptions("shortcut-id"), typeof(ShortcutRunCommandHandler)];
        yield return [new ShortcutCliOptions(ShortcutCliAction.Add), typeof(ShortcutCommandHandler)];
        yield return [new TriggerListCliOptions(), typeof(TriggerListCommandHandler)];
        yield return [new TriggerCliOptions(TriggerCliAction.Add), typeof(TriggerCommandHandler)];
        yield return [new RecordCliOptions("recorded.macro"), typeof(RecordCommandHandler)];
        yield return [new RunCliOptions(["tap A"]), typeof(RunCommandHandler)];
        yield return [new ClipboardCliOptions(ClipboardCliAction.Get), typeof(ClipboardCommandHandler)];
        yield return [new ClipboardCliOptions(ClipboardCliAction.Clear), typeof(ClipboardCommandHandler)];
        yield return [new WindowCliOptions(WindowCliAction.Active), typeof(WindowCommandHandler)];
        yield return [new ScreenCliOptions(ScreenCliAction.Pixel, 1, 2), typeof(ScreenCommandHandler)];
        yield return [new ScreenshotCliOptions(ScreenshotCliAction.Capture, "shot.png"), typeof(ScreenshotCommandHandler)];
        yield return [new HeadlessCliOptions(), typeof(HeadlessCommandHandler)];
    }

    private static HandlerSet CreateHandlers()
    {
        return new HandlerSet(
            new MacroValidateCommandHandler(Substitute.For<IMacroExecutionService>()),
            new MacroInfoCommandHandler(Substitute.For<IMacroExecutionService>()),
            new PlayCommandHandler(Substitute.For<IMacroExecutionService>(), Substitute.For<ICliPreflightService>()),
            new DoctorCommandHandler(Substitute.For<IDoctorService>()),
            new QuickSetupCommandHandler(Substitute.For<IQuickSetupCliService>()),
            new SettingsGetCommandHandler(Substitute.For<ISettingsCliService>()),
            new SettingsSetCommandHandler(Substitute.For<ISettingsCliService>()),
            new SettingsListKeysCommandHandler(Substitute.For<ISettingsCliService>()),
            new SettingsResetCommandHandler(Substitute.For<ISettingsCliService>()),
            new ProfileCommandHandler(Substitute.For<IProfileCliService>()),
            new TextExpansionCommandHandler(Substitute.For<ITextExpansionCliService>()),
            new ScheduleListCommandHandler(Substitute.For<IScheduleCliService>()),
            new ScheduleRunCommandHandler(Substitute.For<IScheduleCliService>()),
            new ScheduleCommandHandler(Substitute.For<IScheduleCliService>()),
            new ShortcutListCommandHandler(Substitute.For<IShortcutCliService>()),
            new ShortcutRunCommandHandler(Substitute.For<IShortcutCliService>()),
            new ShortcutCommandHandler(Substitute.For<IShortcutCliService>()),
            new TriggerListCommandHandler(Substitute.For<ITriggerCliService>()),
            new TriggerCommandHandler(Substitute.For<ITriggerCliService>()),
            new RecordCommandHandler(Substitute.For<IRecordExecutionService>(), Substitute.For<ICliPreflightService>()),
            new RunCommandHandler(Substitute.For<IRunScriptExecutionService>(), Substitute.For<ICliPreflightService>()),
            new ClipboardCommandHandler(Substitute.For<IClipboardCliService>()),
            new WindowCommandHandler(Substitute.For<IWindowCliService>()),
            new ScreenCommandHandler(Substitute.For<IScreenCliService>()),
            new ScreenshotCommandHandler(Substitute.For<IScreenshotCliService>()),
            new HeadlessCommandHandler(Substitute.For<IHeadlessRuntimeService>(), Substitute.For<ICliPreflightService>()));
    }

    private static CliCommandHandlerResolver CreateResolver(HandlerSet handlers)
    {
        return new CliCommandHandlerResolver(
        [
            new CliCommandHandlerRegistration(typeof(MacroValidateCliOptions), () => handlers.MacroValidate),
            new CliCommandHandlerRegistration(typeof(MacroInfoCliOptions), () => handlers.MacroInfo),
            new CliCommandHandlerRegistration(typeof(PlayCliOptions), () => handlers.Play),
            new CliCommandHandlerRegistration(typeof(DoctorCliOptions), () => handlers.Doctor),
            new CliCommandHandlerRegistration(typeof(QuickSetupCliOptions), () => handlers.QuickSetup),
            new CliCommandHandlerRegistration(typeof(SettingsGetCliOptions), () => handlers.SettingsGet),
            new CliCommandHandlerRegistration(typeof(SettingsSetCliOptions), () => handlers.SettingsSet),
            new CliCommandHandlerRegistration(typeof(SettingsListKeysCliOptions), () => handlers.SettingsListKeys),
            new CliCommandHandlerRegistration(typeof(SettingsResetCliOptions), () => handlers.SettingsReset),
            new CliCommandHandlerRegistration(typeof(ProfileCliOptions), () => handlers.Profile),
            new CliCommandHandlerRegistration(typeof(TextExpansionCliOptions), () => handlers.TextExpansion),
            new CliCommandHandlerRegistration(typeof(ScheduleListCliOptions), () => handlers.ScheduleList),
            new CliCommandHandlerRegistration(typeof(ScheduleRunCliOptions), () => handlers.ScheduleRun),
            new CliCommandHandlerRegistration(typeof(ScheduleCliOptions), () => handlers.Schedule),
            new CliCommandHandlerRegistration(typeof(ShortcutListCliOptions), () => handlers.ShortcutList),
            new CliCommandHandlerRegistration(typeof(ShortcutRunCliOptions), () => handlers.ShortcutRun),
            new CliCommandHandlerRegistration(typeof(ShortcutCliOptions), () => handlers.Shortcut),
            new CliCommandHandlerRegistration(typeof(TriggerListCliOptions), () => handlers.TriggerList),
            new CliCommandHandlerRegistration(typeof(TriggerCliOptions), () => handlers.Trigger),
            new CliCommandHandlerRegistration(typeof(RecordCliOptions), () => handlers.Record),
            new CliCommandHandlerRegistration(typeof(RunCliOptions), () => handlers.Run),
            new CliCommandHandlerRegistration(typeof(ClipboardCliOptions), () => handlers.Clipboard),
            new CliCommandHandlerRegistration(typeof(WindowCliOptions), () => handlers.Window),
            new CliCommandHandlerRegistration(typeof(ScreenCliOptions), () => handlers.Screen),
            new CliCommandHandlerRegistration(typeof(ScreenshotCliOptions), () => handlers.Screenshot),
            new CliCommandHandlerRegistration(typeof(HeadlessCliOptions), () => handlers.Headless),
        ]);
    }

    private sealed record UnknownCliOptions() : CliCommandOptions(JsonOutput: false);

    private sealed record HandlerSet(
        MacroValidateCommandHandler MacroValidate,
        MacroInfoCommandHandler MacroInfo,
        PlayCommandHandler Play,
        DoctorCommandHandler Doctor,
        QuickSetupCommandHandler QuickSetup,
        SettingsGetCommandHandler SettingsGet,
        SettingsSetCommandHandler SettingsSet,
        SettingsListKeysCommandHandler SettingsListKeys,
        SettingsResetCommandHandler SettingsReset,
        ProfileCommandHandler Profile,
        TextExpansionCommandHandler TextExpansion,
        ScheduleListCommandHandler ScheduleList,
        ScheduleRunCommandHandler ScheduleRun,
        ScheduleCommandHandler Schedule,
        ShortcutListCommandHandler ShortcutList,
        ShortcutRunCommandHandler ShortcutRun,
        ShortcutCommandHandler Shortcut,
        TriggerListCommandHandler TriggerList,
        TriggerCommandHandler Trigger,
        RecordCommandHandler Record,
        RunCommandHandler Run,
        ClipboardCommandHandler Clipboard,
        WindowCommandHandler Window,
        ScreenCommandHandler Screen,
        ScreenshotCommandHandler Screenshot,
        HeadlessCommandHandler Headless);
}
