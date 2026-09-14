using CrossMacro.Application.Runtime;
using CrossMacro.Application.Automation;
namespace CrossMacro.Infrastructure.Tests.Services;


public sealed class ProfileManagerTests : IDisposable
{
    private static readonly CancellationToken NonCancelableToken = new(canceled: false);
    private readonly string _tempPath;

    public ProfileManagerTests()
    {
        _tempPath = Path.Combine(GetPhysicalTempPath(), "CrossMacroProfileManagerTests_" + Guid.NewGuid());
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
    public async Task CreateProfileAsync_UsesInjectedTimeProviderForCreationMetadata()
    {
        var expected = new DateTimeOffset(2030, 2, 3, 4, 5, 6, TimeSpan.Zero);
        var manager = new ProfileManager(_tempPath, new FakeTimeProvider(expected));
        await manager.InitializeAsync();

        var profile = await manager.CreateProfileAsync("Timed Profile");

        _ = profile.CreatedAt.Should().Be(expected.UtcDateTime);
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
    public async Task InitializeAsync_IsIdempotentAfterRuntimeServicesAreLoaded()
    {
        var settingsService = Substitute.For<ISettingsService>();
        var hotkeyConfigService = Substitute.For<IHotkeyConfigurationService>();
        var schedulerService = Substitute.For<ISchedulerService>();
        var scheduledTaskRepository = Substitute.For<IScheduledTaskRepository>();
        var textExpansionStorageService = Substitute.For<ITextExpansionStorageService>();
        _ = hotkeyConfigService.LoadAsync().Returns(Task.FromResult(new HotkeySettings()));
        _ = schedulerService.Completion.Returns(Task.CompletedTask);

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
        await manager.InitializeAsync();

        _ = manager.IsInitialized.Should().BeTrue();
        _ = settingsService.ReceivedCalls().Count(call =>
            string.Equals(call.GetMethodInfo().Name, nameof(ISettingsService.ReloadAsync), StringComparison.Ordinal)).Should().Be(1);
        _ = hotkeyConfigService.ReceivedCalls().Count(call =>
            string.Equals(call.GetMethodInfo().Name, nameof(IHotkeyConfigurationService.ReloadAsync), StringComparison.Ordinal)).Should().Be(1);
        _ = schedulerService.ReceivedCalls().Count(call =>
            string.Equals(call.GetMethodInfo().Name, nameof(ISchedulerService.LoadAsync), StringComparison.Ordinal)).Should().Be(1);
        _ = scheduledTaskRepository.ReceivedCalls().Count(call =>
            string.Equals(call.GetMethodInfo().Name, nameof(IScheduledTaskRepository.ReloadAsync), StringComparison.Ordinal)).Should().Be(1);
        _ = textExpansionStorageService.ReceivedCalls().Count(call =>
            string.Equals(call.GetMethodInfo().Name, nameof(ITextExpansionStorageService.ReloadAsync), StringComparison.Ordinal)).Should().Be(1);
    }

    [Fact]
    public async Task InitializeAsync_WaitsForAutomationMutationBeforeReloadingProfile()
    {
        using var mutations = new AutomationTaskMutationGate();
        var releaseMutation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var mutation = mutations.RunAsync(() => releaseMutation.Task, CancellationToken.None);
        var settings = Substitute.For<ISettingsService>();
        var hotkeys = Substitute.For<IHotkeyConfigurationService>();
        _ = hotkeys.LoadAsync().Returns(Task.FromResult(new HotkeySettings()));
        using var manager = CreateCoordinator(
            new ProfileManager(_tempPath), settings, hotkeys,
            new HotkeySettings(), hotkeyService: null, shortcutService: null, schedulerService: null, textExpansionService: null,
            Substitute.For<IScheduledTaskRepository>(), Substitute.For<ITextExpansionStorageService>(), mutations);

        var initialization = manager.InitializeAsync();

        _ = initialization.IsCompleted.Should().BeFalse();
        _ = settings.ReceivedCalls().Should().BeEmpty();
        releaseMutation.SetResult();
        await mutation;
        await initialization;
        Assert.Equal(1, mutations.ScopeGeneration);
        await manager.InitializeAsync();
        Assert.Equal(1, mutations.ScopeGeneration);
        _ = manager.IsInitialized.Should().BeTrue();
        await settings.Received(1).ReloadAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task SwitchProfileAsync_PublishesChangedEventAfterReleasingAutomationMutationGate()
    {
        using var mutations = new AutomationTaskMutationGate();
        var settings = Substitute.For<ISettingsService>();
        var hotkeys = Substitute.For<IHotkeyConfigurationService>();
        _ = hotkeys.LoadAsync().Returns(Task.FromResult(new HotkeySettings()));
        using var manager = CreateCoordinator(
            new ProfileManager(_tempPath), settings, hotkeys,
            new HotkeySettings(), hotkeyService: null, shortcutService: null, schedulerService: null, textExpansionService: null,
            Substitute.For<IScheduledTaskRepository>(), Substitute.For<ITextExpansionStorageService>(), mutations);
        await manager.InitializeAsync();
        var profile = await manager.CreateProfileAsync("Other");
        Task? reentrantMutation = null;
        manager.ProfileChanged += (_, _) =>
        {
            reentrantMutation = mutations.RunAsync(() => Task.CompletedTask, CancellationToken.None);
            _ = reentrantMutation.IsCompletedSuccessfully.Should().BeTrue();
        };

        await manager.SwitchProfileAsync(profile.Id);

        _ = reentrantMutation.Should().NotBeNull();
        _ = manager.ActiveProfile.Id.Should().Be(profile.Id);
    }

    [Fact]
    public async Task SwitchProfileAsync_WhenReplacementAndRollbackFail_FaultsTaskScopeAndRuntimeWithoutRestart()
    {
        var settings = Substitute.For<ISettingsService>();
        var hotkeys = Substitute.For<IHotkeyConfigurationService>();
        _ = hotkeys.LoadAsync().Returns(Task.FromResult(new HotkeySettings()));
        using var mutations = new AutomationTaskMutationGate();
        var starts = 0;
        var running = false;
        var runtime = new AutomationRuntimeSession([new AutomationRuntimeComponent("scheduler", () => running,
            _ => { starts++; running = true; return Task.CompletedTask; },
            _ => { running = false; return Task.CompletedTask; })]);
        using var manager = CreateCoordinator(
            new ProfileManager(_tempPath), settings, hotkeys,
            new HotkeySettings(), hotkeyService: null, shortcutService: null, schedulerService: null, textExpansionService: null,
            Substitute.For<IScheduledTaskRepository>(), Substitute.For<ITextExpansionStorageService>(), mutations, runtime);
        await manager.InitializeAsync();
        await runtime.StartAsync(CancellationToken.None);
        var previous = manager.ActiveProfile;
        var profile = await manager.CreateProfileAsync("Other");
        var previousScope = mutations.ScopeGeneration;
        var replacementError = new IOException("replacement failed");
        var rollbackError = new IOException("rollback failed");
        _ = settings.ReloadAsync(manager.GetProfileDirectory(profile.Id)).Returns(Task.FromException(replacementError));
        _ = settings.ReloadAsync(manager.GetProfileDirectory(previous.Id)).Returns(Task.FromException(rollbackError));

        var failure = await Assert.ThrowsAsync<AggregateException>(() => manager.SwitchProfileAsync(profile.Id));

        _ = failure.InnerExceptions.Should().ContainInOrder(replacementError, rollbackError);
        _ = manager.ActiveProfile.Id.Should().Be(previous.Id);
        Assert.True(mutations.ScopeGeneration > previousScope);
        Assert.True(mutations.IsFaulted);
        Assert.True(runtime.IsFaulted);
        Assert.False(running);
        Assert.Equal(1, starts);
        using var tasks = new ManageSchedule(Substitute.For<IScheduledTaskOperations>(), Substitute.For<IScheduledTaskStore>(), mutations);
        _ = await Assert.ThrowsAsync<TaskScopeUnavailableException>(() => tasks.ListAsync(CancellationToken.None));
        _ = await Assert.ThrowsAsync<TaskScopeUnavailableException>(() => tasks.AddAsync(new ScheduledTask(), mutations.ScopeGeneration, CancellationToken.None));
        _ = await Assert.ThrowsAsync<TaskScopeUnavailableException>(() => tasks.RunAsync(new TaskRequest(Guid.NewGuid()), CancellationToken.None));
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.StartAsync(CancellationToken.None));
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.RunSuspendedAsync(() => Task.CompletedTask, CancellationToken.None));
        Assert.Equal(1, starts);
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
        _ = Directory.Exists(Path.Combine(createdDirectory, ConfigFileNames.MacrosDirectory)).Should().BeTrue();
        _ = Directory.Exists(Path.Combine(defaultDirectory, ConfigFileNames.MacrosDirectory)).Should().BeTrue();
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
    public async Task InitializeAsync_WhenConfigRootHasSymlinkedAncestor_UsesConfiguredTarget()
    {
        var outsideRoot = Path.Combine(_tempPath, "outside-target");
        var symlinkedAncestor = Path.Combine(_tempPath, "linked-parent");
        var configRoot = Path.Combine(symlinkedAncestor, "config");
        _ = Directory.CreateDirectory(outsideRoot);
        _ = Directory.CreateSymbolicLink(symlinkedAncestor, outsideRoot);

        var manager = new ProfileManager(configRoot);

        await manager.InitializeAsync();

        _ = manager.ActiveProfile.Id.Should().Be("default");
        _ = Directory.Exists(Path.Combine(outsideRoot, "config", ConfigFileNames.ProfilesDirectory, "default")).Should().BeTrue();
        _ = File.Exists(Path.Combine(outsideRoot, "config", ConfigFileNames.ProfileRegistry)).Should().BeTrue();
    }

    [Fact]
    public async Task GetProfileDirectory_WhenProfileDirectoryIsSymlink_RejectsIt()
    {
        var manager = new ProfileManager(_tempPath);
        await manager.InitializeAsync();
        var outsideRoot = Path.Combine(_tempPath, "outside-target");
        var profilePath = Path.Combine(_tempPath, ConfigFileNames.ProfilesDirectory, "work");
        _ = Directory.CreateDirectory(outsideRoot);
        _ = Directory.CreateSymbolicLink(profilePath, outsideRoot);

        var act = () => manager.GetProfileDirectory("work");

        _ = act.Should().Throw<InvalidDataException>()
            .WithMessage("*must not be a reparse point*");
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
        var schedulerRunning = true;
        _ = schedulerService.IsRunning.Returns(_ => schedulerRunning);
        _ = schedulerService.Completion.Returns(Task.CompletedTask);
        _ = schedulerService.StopAsync(CancellationToken.None).Returns(_ =>
        {
            schedulerRunning = false;
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
        _ = schedulerService.StopAsync(CancellationToken.None).Returns(Task.CompletedTask);

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

    [Fact]
    public async Task SwitchProfileAsync_FlushesAndReloadsProfileRuntimeParticipants()
    {
        var settingsService = Substitute.For<ISettingsService>();
        var hotkeyConfigService = Substitute.For<IHotkeyConfigurationService>();
        var schedulerService = Substitute.For<ISchedulerService>();
        var scheduledTaskRepository = Substitute.For<IScheduledTaskRepository>();
        var textExpansionStorageService = Substitute.For<ITextExpansionStorageService>();
        var participant = Substitute.For<IProfileRuntimeParticipant>();
        _ = hotkeyConfigService.LoadAsync().Returns(Task.FromResult(new HotkeySettings()));
        _ = schedulerService.Completion.Returns(Task.CompletedTask);

        var manager = new ProfileRuntimeCoordinator(
            new ProfileManager(_tempPath),
            settingsService,
            hotkeyConfigService,
            new HotkeySettings(),
            hotkeyService: null,
            shortcutService: null,
            schedulerService,
            textExpansionService: null,
            Substitute.For<ITriggerService>(),
            scheduledTaskRepository,
            textExpansionStorageService,
            runtimeState: null,
            profileRuntimeParticipants: [participant]);

        await manager.InitializeAsync();
        var profile = await manager.CreateProfileAsync("Second Profile");
        await manager.SwitchProfileAsync(profile.Id);

        await participant.Received(1).FlushAsync(CancellationToken.None);
        await participant.Received(1).ReloadAsync(manager.GetProfileDirectory(profile.Id), CancellationToken.None);
    }

    [Fact]
    public async Task SwitchProfileAsync_WhenParticipantReloadFails_RestoresPreviousProfileParticipantState()
    {
        var settingsService = Substitute.For<ISettingsService>();
        var hotkeyConfigService = Substitute.For<IHotkeyConfigurationService>();
        var schedulerService = Substitute.For<ISchedulerService>();
        var scheduledTaskRepository = Substitute.For<IScheduledTaskRepository>();
        var textExpansionStorageService = Substitute.For<ITextExpansionStorageService>();
        var participant = Substitute.For<IProfileRuntimeParticipant>();
        _ = hotkeyConfigService.LoadAsync().Returns(Task.FromResult(new HotkeySettings()));
        _ = schedulerService.Completion.Returns(Task.CompletedTask);
        var catalog = new ProfileManager(_tempPath);
        var manager = new ProfileRuntimeCoordinator(
            catalog,
            settingsService,
            hotkeyConfigService,
            new HotkeySettings(),
            hotkeyService: null,
            shortcutService: null,
            schedulerService,
            textExpansionService: null,
            Substitute.For<ITriggerService>(),
            scheduledTaskRepository,
            textExpansionStorageService,
            runtimeState: null,
            profileRuntimeParticipants: [participant]);

        await manager.InitializeAsync();
        var profile = await manager.CreateProfileAsync("Second Profile");
        var previousProfileDirectory = manager.GetProfileDirectory(manager.ActiveProfile.Id);
        _ = participant.ReloadAsync(manager.GetProfileDirectory(profile.Id), CancellationToken.None)
            .Returns<Task>(_ => throw new InvalidDataException("Invalid loaded macro session."));

        var switchProfile = () => manager.SwitchProfileAsync(profile.Id);

        _ = await switchProfile.Should().ThrowAsync<InvalidDataException>();
        _ = manager.ActiveProfile.Id.Should().Be("default");
        await participant.Received(1).ReloadAsync(previousProfileDirectory, CancellationToken.None);
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

    private static string GetPhysicalTempPath()
    {
        var tempPath = Path.GetFullPath(Path.GetTempPath());
        var tempDirectory = new DirectoryInfo(tempPath);
        for (var current = tempDirectory; current is not null; current = current.Parent)
        {
            if (!current.Exists || !current.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                continue;
            }

            if (current.ResolveLinkTarget(returnFinalTarget: true) is not { FullName: var targetPath })
            {
                break;
            }

            var suffix = Path.GetRelativePath(current.FullName, tempPath);
            return string.Equals(suffix, ".", StringComparison.Ordinal) ? targetPath : Path.Combine(targetPath, suffix);
        }

        return tempPath;
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
        ITextExpansionStorageService textExpansionStorageService,
        AutomationTaskMutationGate? taskMutations = null,
        AutomationRuntimeSession? runtimeSession = null)
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
            textExpansionStorageService,
            taskMutations: taskMutations,
            runtimeSession: runtimeSession);
    }
}
