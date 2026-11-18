namespace CrossMacro.Infrastructure.Tests.Services.Profiles;

internal sealed class ProfileCoordinatorFixture : IDisposable
{
    private readonly bool _ownsMutations;

    public ProfileCoordinatorFixture(
        string configDirectory,
        bool withScheduler = false,
        IReadOnlyList<IProfileRuntimeParticipant>? participants = null,
        AutomationTaskMutationGate? mutations = null,
        AutomationRuntimeSession? runtimeSession = null)
    {
        Settings = SettingsServiceSubstitute.Create();
        Hotkeys = Substitute.For<IHotkeyConfigurationService>();
        _ = Hotkeys.LoadAsync().Returns(Task.FromResult(new HotkeySettings()));
        Scheduler = withScheduler ? Substitute.For<ISchedulerService>() : null;
        _ = Scheduler?.Completion.Returns(Task.CompletedTask);
        ScheduledTasks = Substitute.For<IScheduledTaskRepository>();
        TextExpansions = Substitute.For<ITextExpansionStorageService>();
        _ownsMutations = mutations is null;
        Mutations = mutations ?? new AutomationTaskMutationGate();
        Catalog = new ProfileCatalog(configDirectory);

        var hotkeySettings = new HotkeySettings();
        var triggers = Substitute.For<ITriggerService>();
        Coordinator = new ProfileRuntimeCoordinator(
            Catalog,
            Settings,
            Hotkeys,
            hotkeySettings,
            hotkeyService: null,
            shortcutService: null,
            Scheduler,
            triggers,
            ScheduledTasks,
            TextExpansions,
            new ProfileRuntimeState(),
            participants ?? [],
            runtimeSession ?? AutomationRuntimeSessionFactory.Create(Settings, hotkeySettings, hotkey: null, shortcut: null, Scheduler, triggers, textExpansion: null),
            new SettingsChangeCoordinator(Settings),
            Mutations);
    }

    public ISettingsService Settings { get; }

    public IHotkeyConfigurationService Hotkeys { get; }

    public ISchedulerService? Scheduler { get; }

    public IScheduledTaskRepository ScheduledTasks { get; }

    public ITextExpansionStorageService TextExpansions { get; }

    public AutomationTaskMutationGate Mutations { get; }

    public ProfileCatalog Catalog { get; }

    public ProfileRuntimeCoordinator Coordinator { get; }

    public void Dispose()
    {
        Coordinator.Dispose();
        Catalog.Dispose();
        if (_ownsMutations)
        {
            Mutations.Dispose();
        }
    }
}
