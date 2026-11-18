
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

    public static TheoryData<CommandOptions, Type> KnownCommandOptions => new()
    {
        { new MacroValidateOptions("demo.macro"), typeof(MacroValidateCommandHandler) },
        { new MacroInfoOptions("demo.macro"), typeof(MacroInfoCommandHandler) },
        { new PlayOptions("demo.macro"), typeof(PlayCommandHandler) },
        { new DoctorOptions(), typeof(DoctorCommandHandler) },
        { new QuickSetupOptions(), typeof(QuickSetupCommandHandler) },
        { new SettingsGetOptions(), typeof(SettingsGetCommandHandler) },
        { new SettingsSetOptions("playback.speed", "1.25"), typeof(SettingsSetCommandHandler) },
        { new SettingsListKeysOptions(), typeof(SettingsListKeysCommandHandler) },
        { new SettingsResetOptions("ui.theme"), typeof(SettingsResetCommandHandler) },
        { new ProfileOptions(ProfileAction.List), typeof(ProfileCommandHandler) },
        { new TextExpansionOptions(TextExpansionAction.List), typeof(TextExpansionCommandHandler) },
        { new ScheduleListOptions(), typeof(TaskListCommandHandler<ScheduleListOptions, ScheduledTask, ScheduleCommand>) },
        { new ScheduleRunOptions("task-id"), typeof(TaskRunCommandHandler<ScheduleRunOptions, ScheduledTask, ScheduleCommand>) },
        { new ScheduleOptions(ScheduleAction.Add), typeof(TaskCommandHandler<ScheduleOptions, ScheduledTask, ScheduleCommand>) },
        { new ShortcutListOptions(), typeof(TaskListCommandHandler<ShortcutListOptions, ShortcutTask, ShortcutCommand>) },
        { new ShortcutRunOptions("shortcut-id"), typeof(TaskRunCommandHandler<ShortcutRunOptions, ShortcutTask, ShortcutCommand>) },
        { new ShortcutOptions(ShortcutAction.Add), typeof(TaskCommandHandler<ShortcutOptions, ShortcutTask, ShortcutCommand>) },
        { new TriggerListOptions(), typeof(TaskListCommandHandler<TriggerListOptions, TriggerTask, TriggerCommand>) },
        { new TriggerOptions(TriggerTaskAction.Add), typeof(TaskCommandHandler<TriggerOptions, TriggerTask, TriggerCommand>) },
        { new RecordOptions("recorded.macro"), typeof(RecordCommandHandler) },
        { new RunOptions(["tap A"]), typeof(RunCommandHandler) },
        { new InputOptions("tap A"), typeof(InputCommandHandler) },
        { new ClipboardOptions(ClipboardAction.Get), typeof(ClipboardCommandHandler) },
        { new WindowOptions(WindowAction.Active), typeof(WindowCommandHandler) },
        { new ScreenOptions(ScreenAction.Pixel, 1, 2), typeof(ScreenCommandHandler) },
        { new ScreenshotOptions("shot.png"), typeof(ScreenshotCommandHandler) },
        { new HeadlessOptions(), typeof(HeadlessCommandHandler) },
        { new McpOptions(), typeof(McpCommandHandler) },
    };

    [Theory]
    [MemberData(nameof(KnownCommandOptions))]
    public void Resolve_WhenKnownOptions_ReturnsTheProductionRegisteredHandler(CommandOptions options, Type expectedHandlerType)
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
        var concrete = typeof(CommandOptions).Assembly.GetTypes()
            .Where(type => type is { IsAbstract: false } && type.IsSubclassOf(typeof(CommandOptions)))
            .ToHashSet();

        Assert.Empty(concrete.Except(covered));
    }

    [Fact]
    public void Resolve_WhenUnknownOptions_ReturnsNull()
    {
        var resolver = _provider.GetRequiredService<ICliCommandHandlerResolver>();

        Assert.Null(resolver.Resolve(new UnknownOptions()));
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
                typeof(MacroValidateOptions),
                () =>
                {
                    requestedFactoryCalls++;
                    return requestedHandler;
                }),
            new CliCommandHandlerRegistration(
                typeof(MacroInfoOptions),
                () =>
                {
                    unrelatedFactoryCalls++;
                    return Substitute.For<ICliCommandHandler>();
                }),
        ]);

        var handler = resolver.Resolve(new MacroValidateOptions("demo.macro"));

        Assert.Same(requestedHandler, handler);
        Assert.Equal(1, requestedFactoryCalls);
        Assert.Equal(0, unrelatedFactoryCalls);
    }

    [Fact]
    public void Create_WhenMultipleHandlersAreRegisteredForOptionsType_ThrowsClearly()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new CliCommandHandlerResolver(
        [
            new CliCommandHandlerRegistration(typeof(MacroValidateOptions), () => Substitute.For<ICliCommandHandler>()),
            new CliCommandHandlerRegistration(typeof(MacroValidateOptions), () => Substitute.For<ICliCommandHandler>()),
        ]));

        Assert.Contains(nameof(MacroValidateOptions), exception.Message, StringComparison.Ordinal);
    }

    private sealed record UnknownOptions() : CommandOptions(JsonOutput: false);
}
