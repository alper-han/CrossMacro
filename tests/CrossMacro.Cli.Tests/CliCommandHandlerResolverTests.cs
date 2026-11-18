
namespace CrossMacro.Cli.Tests;

public sealed class CliCommandHandlerResolverTests : IAsyncDisposable
{
    private readonly TempDirectory _configDirectory = new(create: false);
    private readonly ServiceProvider _provider;

    public CliCommandHandlerResolverTests()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(Substitute.For<IClipboardService>());
        _ = services.AddSingleton(Substitute.For<IImageClipboardService>());
        _ = services.AddSingleton(Substitute.For<IImageClipboardReader>());
        _ = services.AddSingleton(new ApplicationPaths(_configDirectory.Path));
        _ = services.AddCrossMacroCliRuntimeServices(new TestPlatformServiceRegistrar(), CliRuntimeProfile.OneShot);
        _ = services.AddCliServices();
        _provider = services.BuildServiceProvider();
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        _configDirectory.Dispose();
    }

    public static TheoryData<CliCommandOptions, Type> KnownCommandOptions => new()
    {
        { new MacroValidateCliOptions("demo.macro"), typeof(MacroValidateCommandHandler) },
        { new MacroInfoCliOptions("demo.macro"), typeof(MacroInfoCommandHandler) },
        { new PlayCliOptions("demo.macro"), typeof(PlayCommandHandler) },
        { new DoctorCliOptions(), typeof(DoctorCommandHandler) },
        { new QuickSetupCliOptions(), typeof(QuickSetupCommandHandler) },
        { new SettingsGetCliOptions(), typeof(SettingsGetCommandHandler) },
        { new SettingsSetCliOptions("playback.speed", "1.25"), typeof(SettingsSetCommandHandler) },
        { new SettingsListKeysCliOptions(), typeof(SettingsListKeysCommandHandler) },
        { new SettingsResetCliOptions("ui.theme"), typeof(SettingsResetCommandHandler) },
        { new ProfileCliOptions(ProfileCliAction.List), typeof(ProfileCommandHandler) },
        { new TextExpansionCliOptions(TextExpansionCliAction.List), typeof(TextExpansionCommandHandler) },
        { new ScheduleListCliOptions(), typeof(TaskListCommandHandler<ScheduleListCliOptions, ScheduledTask, ScheduleCommand>) },
        { new ScheduleRunCliOptions("task-id"), typeof(TaskRunCommandHandler<ScheduleRunCliOptions, ScheduledTask, ScheduleCommand>) },
        { new ScheduleCliOptions(ScheduleCliAction.Add), typeof(TaskCommandHandler<ScheduleCliOptions, ScheduledTask, ScheduleCommand>) },
        { new ShortcutListCliOptions(), typeof(TaskListCommandHandler<ShortcutListCliOptions, ShortcutTask, ShortcutCommand>) },
        { new ShortcutRunCliOptions("shortcut-id"), typeof(TaskRunCommandHandler<ShortcutRunCliOptions, ShortcutTask, ShortcutCommand>) },
        { new ShortcutCliOptions(ShortcutCliAction.Add), typeof(TaskCommandHandler<ShortcutCliOptions, ShortcutTask, ShortcutCommand>) },
        { new TriggerListCliOptions(), typeof(TaskListCommandHandler<TriggerListCliOptions, TriggerTask, TriggerCommand>) },
        { new TriggerCliOptions(TriggerCliAction.Add), typeof(TaskCommandHandler<TriggerCliOptions, TriggerTask, TriggerCommand>) },
        { new RecordCliOptions("recorded.macro"), typeof(RecordCommandHandler) },
        { new RunCliOptions(["tap A"]), typeof(RunCommandHandler) },
        { new InputCliOptions("tap A"), typeof(InputCommandHandler) },
        { new ClipboardCliOptions(ClipboardCliAction.Get), typeof(ClipboardCommandHandler) },
        { new WindowCliOptions(WindowCliAction.Active), typeof(WindowCommandHandler) },
        { new ScreenCliOptions(ScreenCliAction.Pixel, 1, 2), typeof(ScreenCommandHandler) },
        { new ScreenshotCliOptions("shot.png"), typeof(ScreenshotCommandHandler) },
        { new HeadlessCliOptions(), typeof(HeadlessCommandHandler) },
        { new McpCliOptions(), typeof(McpCommandHandler) },
    };

    [Theory]
    [MemberData(nameof(KnownCommandOptions))]
    public void Resolve_WhenKnownOptions_ReturnsTheProductionRegisteredHandler(CliCommandOptions options, Type expectedHandlerType)
    {
        var resolver = _provider.GetRequiredService<ICliCommandHandlerResolver>();

        var handler = resolver.Resolve(options);

        Assert.NotNull(handler);
        Assert.IsType(expectedHandlerType, handler);
        Assert.Same(handler, resolver.Resolve(options));
    }

    [Fact]
    public void EveryConcreteCommandOptionsType_IsCoveredByTheResolutionTheory()
    {
        var covered = KnownCommandOptions.Select(row => row.Data.Item1.GetType()).ToHashSet();
        var concrete = typeof(CliCommandOptions).Assembly.GetTypes()
            .Where(type => type is { IsAbstract: false } && type.IsSubclassOf(typeof(CliCommandOptions)))
            .ToHashSet();

        Assert.Empty(concrete.Except(covered));
    }

    [Fact]
    public void Resolve_WhenUnknownOptions_ReturnsNull()
    {
        var resolver = _provider.GetRequiredService<ICliCommandHandlerResolver>();

        Assert.Null(resolver.Resolve(new UnknownCliOptions()));
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

    private sealed record UnknownCliOptions() : CliCommandOptions(JsonOutput: false);
}
