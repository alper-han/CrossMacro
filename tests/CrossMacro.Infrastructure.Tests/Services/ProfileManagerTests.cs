namespace CrossMacro.Infrastructure.Tests.Services;


public sealed class ProfileManagerTests : IDisposable
{
    private static readonly CancellationToken NonCancelableToken = new(canceled: false);
    private readonly string _tempPath;

    public ProfileManagerTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), "CrossMacroProfileManagerTests_" + Guid.NewGuid());
        _ = Directory.CreateDirectory(_tempPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempPath))
        {
            Directory.Delete(_tempPath, recursive: true);
        }
    }

    [Fact]
    public void Dispose_CanBeCalledRepeatedly()
    {
        var manager = new ProfileManager(_tempPath);

        var act = () =>
        {
            manager.Dispose();
            manager.Dispose();
        };

        _ = act.Should().NotThrow();
    }

    [Fact]
    public async Task CreateProfileAsync_WhenDefaultProfileHasUserData_CreatesCleanDefaultProfile()
    {
        var manager = new ProfileManager(_tempPath);
        await manager.InitializeAsync();

        var defaultDirectory = manager.GetProfileDirectory("default");
        await WriteJsonAsync(
            Path.Combine(defaultDirectory, ConfigFileNames.Settings),
            new ProfileSettings
            {
                PlaybackSpeed = 2.5,
                IsLooping = true,
                EnableTextExpansion = true,
                CheckForUpdates = true,
            },
            CrossMacroJsonContext.Default.ProfileSettings);
        await WriteJsonAsync(
            Path.Combine(defaultDirectory, ConfigFileNames.Hotkeys),
            new HotkeySettings
            {
                RecordingHotkey = "Ctrl+Alt+R",
                PlaybackHotkey = "Ctrl+Alt+P",
                PauseHotkey = "Ctrl+Alt+Space",
            },
            CrossMacroJsonContext.Default.HotkeySettings);
        await WriteJsonAsync(
            Path.Combine(defaultDirectory, ConfigFileNames.Shortcuts),
            new List<ShortcutTask> { new() { Name = "Copied shortcut" } },
            CrossMacroJsonContext.Default.ListShortcutTask);
        await WriteJsonAsync(
            Path.Combine(defaultDirectory, ConfigFileNames.Schedules),
            new List<ScheduledTask> { new() { Name = "Copied schedule" } },
            CrossMacroJsonContext.Default.ListScheduledTask);
        await WriteJsonAsync(
            Path.Combine(defaultDirectory, ConfigFileNames.TextExpansions),
            new List<TextExpansionEntry> { new(":mail", "me@example.com") },
            CrossMacroJsonContext.Default.ListTextExpansionEntry);

        var created = await manager.CreateProfileAsync("Clean Profile");
        var createdDirectory = manager.GetProfileDirectory(created.Id);

        var profileSettings = await ReadJsonAsync(
            Path.Combine(createdDirectory, ConfigFileNames.Settings),
            CrossMacroJsonContext.Default.ProfileSettings);
        var hotkeys = await ReadJsonAsync(
            Path.Combine(createdDirectory, ConfigFileNames.Hotkeys),
            CrossMacroJsonContext.Default.HotkeySettings);
        var shortcuts = await ReadJsonAsync(
            Path.Combine(createdDirectory, ConfigFileNames.Shortcuts),
            CrossMacroJsonContext.Default.ListShortcutTask);
        var schedules = await ReadJsonAsync(
            Path.Combine(createdDirectory, ConfigFileNames.Schedules),
            CrossMacroJsonContext.Default.ListScheduledTask);
        var expansions = await ReadJsonAsync(
            Path.Combine(createdDirectory, ConfigFileNames.TextExpansions),
            CrossMacroJsonContext.Default.ListTextExpansionEntry);

        _ = profileSettings.Should().BeEquivalentTo(new ProfileSettings());
        _ = hotkeys.Should().BeEquivalentTo(new HotkeySettings());
        _ = shortcuts.Should().BeEmpty();
        _ = schedules.Should().BeEmpty();
        _ = expansions.Should().BeEmpty();
    }

    [Fact]
    public async Task InitializeAsync_WhenRegistryContainsTraversalProfileId_RejectsItBeforePathUse()
    {
        var registryPath = Path.Combine(_tempPath, ConfigFileNames.ProfileRegistry);
        await WriteJsonAsync(
            registryPath,
            new ProfileRegistry
            {
                Profiles = { new ProfileInfo { Id = "../outside", Name = "Outside" } },
            },
            CrossMacroJsonContext.Default.ProfileRegistry);

        var manager = new ProfileManager(_tempPath);

        var act = () => manager.InitializeAsync();

        _ = await act.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*unsupported path characters*");
        _ = Directory.Exists(Path.Combine(_tempPath, "outside")).Should().BeFalse();
    }

    [Fact]
    public async Task InitializeAsync_WhenConfigRootHasSymlinkedAncestor_RejectsBeforeCreatingOutsideRoot()
    {
        var outsideRoot = Path.Combine(_tempPath, "outside-target");
        var symlinkedAncestor = Path.Combine(_tempPath, "linked-parent");
        var configRoot = Path.Combine(symlinkedAncestor, "config");
        _ = Directory.CreateDirectory(outsideRoot);
        _ = Directory.CreateSymbolicLink(symlinkedAncestor, outsideRoot);

        var manager = new ProfileManager(configRoot);

        var act = () => manager.InitializeAsync();

        _ = await act.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*reparse point*");
        _ = Directory.Exists(Path.Combine(outsideRoot, "config")).Should().BeFalse();
        _ = File.Exists(Path.Combine(outsideRoot, ConfigFileNames.ProfileRegistry)).Should().BeFalse();
    }

    [Fact]
    public async Task InitializeAsync_MigratesLegacyTriggersIntoDefaultProfile()
    {
        await WriteJsonAsync(
            Path.Combine(_tempPath, ConfigFileNames.Settings),
            new AppSettings(),
            CrossMacroJsonContext.Default.AppSettings);
        var trigger = new TriggerTask
        {
            Name = "Legacy trigger",
            Field = TriggerField.WindowTitle,
            MatchMode = TriggerMatchMode.Contains,
            Value = "Editor",
            Action = TriggerOperation.SwitchProfile,
        };
        await WriteJsonAsync(
            Path.Combine(_tempPath, ConfigFileNames.Triggers),
            new List<TriggerTask> { trigger },
            CrossMacroJsonContext.Default.ListTriggerTask);

        var manager = new ProfileManager(_tempPath);
        await manager.InitializeAsync();

        var migratedPath = Path.Combine(
            manager.GetProfileDirectory("default"),
            ConfigFileNames.Triggers);
        var migrated = await ReadJsonAsync(
            migratedPath,
            CrossMacroJsonContext.Default.ListTriggerTask);

        _ = migrated.Should().ContainSingle();
        _ = migrated[0].Should().BeEquivalentTo(trigger);
    }

    [Fact]
    public async Task SwitchProfileAsync_AwaitsSchedulerShutdownBeforeReloadAndRestart()
    {
        var settingsService = Substitute.For<ISettingsService>();
        var hotkeyConfigService = Substitute.For<IHotkeyConfigurationService>();
        var schedulerService = Substitute.For<ISchedulerService>();
        var scheduledTaskRepository = Substitute.For<IScheduledTaskRepository>();
        var textExpansionStorageService = Substitute.For<ITextExpansionStorageService>();
        var order = new List<string>();

        _ = hotkeyConfigService.LoadAsync().Returns(Task.FromResult(new HotkeySettings()));
        _ = schedulerService.IsRunning.Returns(returnThis: true);
        _ = schedulerService.Completion.Returns(Task.CompletedTask);
        _ = schedulerService.StopAsync().Returns(_ =>
        {
            order.Add("stop");
            return Task.CompletedTask;
        });
        _ = schedulerService.LoadAsync().Returns(_ =>
        {
            order.Add("load");
            return Task.CompletedTask;
        });
        schedulerService.When(service => service.Start()).Do(_ => order.Add("start"));

        var manager = CreateCoordinator(
            new ProfileManager(_tempPath),
            settingsService,
            hotkeyConfigService,
            new HotkeySettings(),
            hotkeyService: null,
            shortcutService: null,
            schedulerService,
            textExpansionService: null,
            scheduledTaskRepository,
            textExpansionStorageService);

        await manager.InitializeAsync();
        var profile = await manager.CreateProfileAsync("Second Profile");

        await manager.SwitchProfileAsync(profile.Id);

        _ = order.Should().ContainInOrder("stop", "load", "start");
    }

    [Fact]
    public async Task SwitchProfileAsync_AbortsWhenSchedulerLifetimeRemainsUnresolved()
    {
        var settingsService = Substitute.For<ISettingsService>();
        var hotkeyConfigService = Substitute.For<IHotkeyConfigurationService>();
        var schedulerService = Substitute.For<ISchedulerService>();
        var scheduledTaskRepository = Substitute.For<IScheduledTaskRepository>();
        var textExpansionStorageService = Substitute.For<ITextExpansionStorageService>();
        var unresolvedLifetime = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _ = hotkeyConfigService.LoadAsync().Returns(Task.FromResult(new HotkeySettings()));
        _ = schedulerService.IsRunning.Returns(returnThis: true);
        _ = schedulerService.Completion.Returns(unresolvedLifetime.Task);
        _ = schedulerService.StopAsync().Returns(Task.CompletedTask);

        var manager = CreateCoordinator(
            new ProfileManager(_tempPath),
            settingsService,
            hotkeyConfigService,
            new HotkeySettings(),
            hotkeyService: null,
            shortcutService: null,
            schedulerService,
            textExpansionService: null,
            scheduledTaskRepository,
            textExpansionStorageService);

        await manager.InitializeAsync();
        var profile = await manager.CreateProfileAsync("Second Profile");
        var loadsBeforeSwitch = schedulerService.ReceivedCalls()
            .Count(call => string.Equals(call.GetMethodInfo().Name, nameof(ISchedulerService.LoadAsync), StringComparison.Ordinal));

        var act = () => manager.SwitchProfileAsync(profile.Id);

        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*scheduler did not quiesce*");
        var loadsAfterSwitch = schedulerService.ReceivedCalls()
            .Count(call => string.Equals(call.GetMethodInfo().Name, nameof(ISchedulerService.LoadAsync), StringComparison.Ordinal));
        _ = loadsAfterSwitch.Should().Be(loadsBeforeSwitch);
        schedulerService.DidNotReceive().Start();
        _ = unresolvedLifetime.TrySetResult();
    }

    private static async Task WriteJsonAsync<T>(string filePath, T value, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
    {
        _ = Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var json = JsonSerializer.Serialize(value, typeInfo);
        await File.WriteAllTextAsync(filePath, json, NonCancelableToken);
    }

    private static async Task<T> ReadJsonAsync<T>(string filePath, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
    {
        var json = await File.ReadAllTextAsync(filePath, NonCancelableToken);
        return JsonSerializer.Deserialize(json, typeInfo)!;
    }

    private static ProfileRuntimeCoordinator CreateCoordinator(
        IProfileCatalog catalog,
        ISettingsService settingsService,
        IHotkeyConfigurationService hotkeyConfigService,
        HotkeySettings hotkeySettings,
        IGlobalHotkeyService? hotkeyService,
        IShortcutService? shortcutService,
        ISchedulerService? schedulerService,
        ITextExpansionService? textExpansionService,
        IScheduledTaskRepository scheduledTaskRepository,
        ITextExpansionStorageService textExpansionStorageService)
    {
        return new ProfileRuntimeCoordinator(
            catalog,
            settingsService,
            hotkeyConfigService,
            hotkeySettings,
            hotkeyService,
            shortcutService,
            schedulerService,
            textExpansionService,
            Substitute.For<ITriggerService>(),
            scheduledTaskRepository,
            textExpansionStorageService);
    }
}
