using System.Collections.Generic;
using CrossMacro.Cli;
using CrossMacro.Cli.Commands;
using CrossMacro.Cli.Services;
using NSubstitute;

namespace CrossMacro.Cli.Tests;

public class CliCommandHandlerResolverTests
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

    public static IEnumerable<object[]> KnownCommandOptions()
    {
        yield return [new MacroValidateCliOptions("demo.macro"), typeof(MacroValidateCommandHandler)];
        yield return [new MacroInfoCliOptions("demo.macro"), typeof(MacroInfoCommandHandler)];
        yield return [new PlayCliOptions("demo.macro"), typeof(PlayCommandHandler)];
        yield return [new DoctorCliOptions(), typeof(DoctorCommandHandler)];
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
            () => handlers.MacroValidate,
            () => handlers.MacroInfo,
            () => handlers.Play,
            () => handlers.Doctor,
            () => handlers.SettingsGet,
            () => handlers.SettingsSet,
            () => handlers.SettingsListKeys,
            () => handlers.SettingsReset,
            () => handlers.Profile,
            () => handlers.TextExpansion,
            () => handlers.ScheduleList,
            () => handlers.ScheduleRun,
            () => handlers.Schedule,
            () => handlers.ShortcutList,
            () => handlers.ShortcutRun,
            () => handlers.Shortcut,
            () => handlers.TriggerList,
            () => handlers.Trigger,
            () => handlers.Record,
            () => handlers.Run,
            () => handlers.Clipboard,
            () => handlers.Window,
            () => handlers.Screen,
            () => handlers.Screenshot,
            () => handlers.Headless);
    }

    private sealed record UnknownCliOptions() : CliCommandOptions(JsonOutput: false);

    private sealed record HandlerSet(
        MacroValidateCommandHandler MacroValidate,
        MacroInfoCommandHandler MacroInfo,
        PlayCommandHandler Play,
        DoctorCommandHandler Doctor,
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
