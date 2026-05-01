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
        yield return [new ScheduleListCliOptions(), typeof(ScheduleListCommandHandler)];
        yield return [new ScheduleRunCliOptions("task-id"), typeof(ScheduleRunCommandHandler)];
        yield return [new ShortcutListCliOptions(), typeof(ShortcutListCommandHandler)];
        yield return [new ShortcutRunCliOptions("shortcut-id"), typeof(ShortcutRunCommandHandler)];
        yield return [new RecordCliOptions("recorded.macro"), typeof(RecordCommandHandler)];
        yield return [new RunCliOptions(["tap A"]), typeof(RunCommandHandler)];
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
            new ScheduleListCommandHandler(Substitute.For<IScheduleCliService>()),
            new ScheduleRunCommandHandler(Substitute.For<IScheduleCliService>()),
            new ShortcutListCommandHandler(Substitute.For<IShortcutCliService>()),
            new ShortcutRunCommandHandler(Substitute.For<IShortcutCliService>()),
            new RecordCommandHandler(Substitute.For<IRecordExecutionService>(), Substitute.For<ICliPreflightService>()),
            new RunCommandHandler(Substitute.For<IRunScriptExecutionService>(), Substitute.For<ICliPreflightService>()),
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
            () => handlers.ScheduleList,
            () => handlers.ScheduleRun,
            () => handlers.ShortcutList,
            () => handlers.ShortcutRun,
            () => handlers.Record,
            () => handlers.Run,
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
        ScheduleListCommandHandler ScheduleList,
        ScheduleRunCommandHandler ScheduleRun,
        ShortcutListCommandHandler ShortcutList,
        ShortcutRunCommandHandler ShortcutRun,
        RecordCommandHandler Record,
        RunCommandHandler Run,
        HeadlessCommandHandler Headless);
}
