using CrossMacro.Application.Runtime;
using CrossMacro.Application.Automation;
using CrossMacro.Application.Settings;
namespace CrossMacro.Infrastructure.Services.Profiles;

public sealed class ProfileRuntimeCoordinator : IProfileManager, IProfileSwitchRequestHandler, IDisposable
{
    private readonly IProfileCatalog _catalog;
    private readonly ISettingsService _settingsService;
    private readonly IHotkeyConfigurationService _hotkeyConfigService;
    private readonly HotkeySettings _hotkeySettings;
    private readonly IGlobalHotkeyService? _hotkeyService;
    private readonly IShortcutService? _shortcutService;
    private readonly ISchedulerService? _schedulerService;
    private readonly ITriggerService _triggerService;
    private readonly IScheduledTaskRepository _scheduledTaskRepository;
    private readonly ITextExpansionStorageService _textExpansionStorageService;
    private readonly ProfileRuntimeState? _runtimeState;
    private readonly IReadOnlyList<IProfileRuntimeParticipant> _profileRuntimeParticipants;
    private readonly AutomationRuntimeSession _runtimeSession;
    private readonly SettingsChangeCoordinator _settingsChanges;
    private readonly AutomationTaskMutationGate _taskMutations;
    private readonly bool _ownsTaskMutations;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _initialized;
    private int _disposed;

    internal ProfileRuntimeCoordinator(
        IProfileCatalog catalog,
        ISettingsService settingsService,
        IHotkeyConfigurationService hotkeyConfigService,
        HotkeySettings hotkeySettings,
        IGlobalHotkeyService? hotkeyService,
        IShortcutService? shortcutService,
        ISchedulerService? schedulerService,
        ITextExpansionService? textExpansionService,
        ITriggerService triggerService,
        IScheduledTaskRepository scheduledTaskRepository,
        ITextExpansionStorageService textExpansionStorageService,
        ProfileRuntimeState? runtimeState = null,
        IEnumerable<IProfileRuntimeParticipant>? profileRuntimeParticipants = null,
        AutomationRuntimeSession? runtimeSession = null,
        SettingsChangeCoordinator? settingsChanges = null,
        AutomationTaskMutationGate? taskMutations = null)
    {
        _ownsTaskMutations = taskMutations is null;
        _taskMutations = taskMutations ?? new AutomationTaskMutationGate();
        _settingsChanges = settingsChanges ?? new SettingsChangeCoordinator(settingsService);
        _runtimeSession = runtimeSession ?? AutomationRuntimeSessionFactory.Create(settingsService, hotkeySettings, hotkeyService, shortcutService, schedulerService, triggerService, textExpansionService);
        _catalog = catalog;
        _settingsService = settingsService;
        _hotkeyConfigService = hotkeyConfigService;
        _hotkeySettings = hotkeySettings;
        _hotkeyService = hotkeyService;
        _shortcutService = shortcutService;
        _schedulerService = schedulerService;
        _triggerService = triggerService;
        _scheduledTaskRepository = scheduledTaskRepository;
        _textExpansionStorageService = textExpansionStorageService;
        _runtimeState = runtimeState;
        _profileRuntimeParticipants = profileRuntimeParticipants?.ToArray() ?? [];
    }

    public ProfileInfo ActiveProfile => _catalog.ActiveProfile;
    public IReadOnlyList<ProfileInfo> Profiles => _catalog.Profiles;
    public bool IsInitialized => Volatile.Read(ref _initialized) is 1;
    public event EventHandler<ProfileChangedEventArgs>? ProfileChanged;

    public Task InitializeAsync() =>
        _taskMutations.RunAsync(InitializeCoreAsync, CancellationToken.None);

    private async Task InitializeCoreAsync()
    {
        await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _initialized) is 1)
            {
                return;
            }

            _ = _taskMutations.AdvanceScopeGeneration();
            await _catalog.InitializeAsync().ConfigureAwait(false);
            await ReloadProfileServicesAsync(_catalog.GetProfileDirectory(_catalog.ActiveProfile.Id)).ConfigureAwait(false);
            Volatile.Write(ref _initialized, 1);
            _runtimeState?.MarkInitialized();
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    public Task HandleSwitchRequestAsync(string profileId) => SwitchProfileAsync(profileId);

    public async Task SwitchProfileAsync(string profileId)
    {
        var activeProfile = await _taskMutations.RunAsync(() => SwitchProfileCoreAsync(profileId), CancellationToken.None).ConfigureAwait(false);
        if (activeProfile is not null)
        {
            ProfileChanged?.Invoke(this, new ProfileChangedEventArgs(activeProfile));
        }
    }

    private async Task<ProfileInfo?> SwitchProfileCoreAsync(string profileId)
    {

        await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            var previousProfile = _catalog.ActiveProfile;
            var profile = _catalog.Profiles.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, profileId, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Profile '{profileId}' does not exist.");

            if (string.Equals(profile.Id, previousProfile.Id, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            _ = _taskMutations.AdvanceScopeGeneration();
            var profileDir = _catalog.GetProfileDirectory(profile.Id);
            await FlushProfileRuntimeParticipantsAsync().ConfigureAwait(false);
            await _runtimeSession.RunSuspendedAsync(async () =>
            {
                _settingsChanges.InvalidatePendingChanges();
                try
                {
                    await ReloadProfileServicesAsync(profileDir).ConfigureAwait(false);
                    await ReloadProfileRuntimeParticipantsAsync(profileDir).ConfigureAwait(false);
                    await _catalog.SetActiveProfileAsync(profile.Id).ConfigureAwait(false);
                }
                catch (Exception error) when (error is not OutOfMemoryException)
                {
                    try
                    {
                        _catalog.RestoreActiveProfile(previousProfile.Id);
                        var previousDirectory = _catalog.GetProfileDirectory(previousProfile.Id);
                        await ReloadProfileServicesAsync(previousDirectory).ConfigureAwait(false);
                        await ReloadProfileRuntimeParticipantsAsync(previousDirectory).ConfigureAwait(false);
                    }
                    catch (Exception rollbackError) when (rollbackError is not OutOfMemoryException)
                    {
                        _taskMutations.MarkFaulted();
                        _runtimeSession.MarkFaulted();
                        throw new AggregateException("Profile replacement and rollback failed.", error, rollbackError);
                    }
                    throw;
                }
            }, CancellationToken.None).ConfigureAwait(false);
            Log.Information("Switched active profile to {ProfileId}", profile.Id);
            return _catalog.ActiveProfile;
        }
        finally
        {
            _ = _gate.Release();
        }

    }

    public async Task<ProfileInfo> CreateProfileAsync(string displayName)
    {
        await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try { return await _catalog.CreateProfileAsync(displayName).ConfigureAwait(false); }
        finally { _ = _gate.Release(); }
    }

    public async Task RenameProfileAsync(string profileId, string newDisplayName)
    {
        await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try { await _catalog.RenameProfileAsync(profileId, newDisplayName).ConfigureAwait(false); }
        finally { _ = _gate.Release(); }
    }

    public async Task DeleteProfileAsync(string profileId)
    {
        await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try { await _catalog.DeleteProfileAsync(profileId).ConfigureAwait(false); }
        finally { _ = _gate.Release(); }
    }

    public string GetProfileDirectory(string profileId) => _catalog.GetProfileDirectory(profileId);

    private async Task ReloadProfileServicesAsync(string profileDir)
    {
        await _settingsService.ReloadAsync(profileDir).ConfigureAwait(false);
        var loaded = await _hotkeyConfigService.ReloadAsync(profileDir).ConfigureAwait(false)
            ?? await _hotkeyConfigService.LoadAsync().ConfigureAwait(false);
        _hotkeySettings.RecordingHotkey = loaded.RecordingHotkey;
        _hotkeySettings.PlaybackHotkey = loaded.PlaybackHotkey;
        _hotkeySettings.PauseHotkey = loaded.PauseHotkey;
        _hotkeyService?.ApplyHotkeys(_hotkeySettings.RecordingHotkey, _hotkeySettings.PlaybackHotkey, _hotkeySettings.PauseHotkey);
        if (_shortcutService is not null)
        {
            await _shortcutService.ReloadAsync(profileDir).ConfigureAwait(false);
        }
        await _triggerService.ReloadAsync(profileDir).ConfigureAwait(false);
        await _scheduledTaskRepository.ReloadAsync(profileDir).ConfigureAwait(false);
        if (_schedulerService is not null)
        {
            await _schedulerService.LoadAsync().ConfigureAwait(false);
        }
        await _textExpansionStorageService.ReloadAsync(profileDir).ConfigureAwait(false);
    }

    private async Task FlushProfileRuntimeParticipantsAsync()
    {
        foreach (var participant in _profileRuntimeParticipants)
        {
            await participant.FlushAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task ReloadProfileRuntimeParticipantsAsync(string profileConfigDirectory)
    {
        foreach (var participant in _profileRuntimeParticipants)
        {
            await participant.ReloadAsync(profileConfigDirectory, CancellationToken.None).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) is 0)
        {
            _gate.Dispose();
            if (_ownsTaskMutations) { _taskMutations.Dispose(); }
        }
    }
}
