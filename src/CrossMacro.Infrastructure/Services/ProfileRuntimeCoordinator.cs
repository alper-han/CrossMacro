namespace CrossMacro.Infrastructure.Services;

public sealed class ProfileRuntimeCoordinator : IProfileManager, IProfileSwitchRequestHandler, IDisposable
{
    private readonly IProfileCatalog _catalog;
    private readonly ISettingsService _settingsService;
    private readonly IHotkeyConfigurationService _hotkeyConfigService;
    private readonly HotkeySettings _hotkeySettings;
    private readonly IGlobalHotkeyService? _hotkeyService;
    private readonly IShortcutService? _shortcutService;
    private readonly ISchedulerService? _schedulerService;
    private readonly ITextExpansionService? _textExpansionService;
    private readonly ITriggerService _triggerService;
    private readonly IScheduledTaskRepository _scheduledTaskRepository;
    private readonly ITextExpansionStorageService _textExpansionStorageService;
    private readonly SemaphoreSlim _gate = new(1, 1);
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
        ITextExpansionStorageService textExpansionStorageService)
    {
        _catalog = catalog;
        _settingsService = settingsService;
        _hotkeyConfigService = hotkeyConfigService;
        _hotkeySettings = hotkeySettings;
        _hotkeyService = hotkeyService;
        _shortcutService = shortcutService;
        _schedulerService = schedulerService;
        _textExpansionService = textExpansionService;
        _triggerService = triggerService;
        _scheduledTaskRepository = scheduledTaskRepository;
        _textExpansionStorageService = textExpansionStorageService;
    }

    public ProfileInfo ActiveProfile => _catalog.ActiveProfile;
    public IReadOnlyList<ProfileInfo> Profiles => _catalog.Profiles;
    public event EventHandler<ProfileChangedEventArgs>? ProfileChanged;

    public async Task InitializeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await _catalog.InitializeAsync().ConfigureAwait(false);
            await ReloadProfileServicesAsync(_catalog.GetProfileDirectory(_catalog.ActiveProfile.Id)).ConfigureAwait(false);
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    public Task HandleSwitchRequestAsync(string profileId) => SwitchProfileAsync(profileId);

    public async Task SwitchProfileAsync(string profileId)
    {
        ProfileInfo activeProfile;

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var previousProfile = _catalog.ActiveProfile;
            var profile = _catalog.Profiles.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, profileId, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Profile '{profileId}' does not exist.");

            if (string.Equals(profile.Id, previousProfile.Id, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var profileDir = _catalog.GetProfileDirectory(profile.Id);
            var hotkeyWasRunning = _hotkeyService?.IsRunning ?? false;
            var shortcutWasListening = _shortcutService?.IsListening ?? false;
            var schedulerWasRunning = _schedulerService?.IsRunning ?? false;
            var textExpansionWasRunning = _textExpansionService?.IsRunning ?? false;
            var triggerWasMonitoring = _triggerService.IsMonitoring;

            if (!await StopRuntimeServicesAsync().ConfigureAwait(false))
            {
                await RestartRuntimeServicesAsync(
                    hotkeyWasRunning,
                    shortcutWasListening,
                    schedulerWasRunning: false,
                    textExpansionWasRunning,
                    triggerWasMonitoring).ConfigureAwait(false);
                throw new InvalidOperationException("Profile switch aborted because the scheduler did not quiesce.");
            }

            try
            {
                await ReloadProfileServicesAsync(profileDir).ConfigureAwait(false);
                await _catalog.SetActiveProfileAsync(profile.Id).ConfigureAwait(false);
                activeProfile = _catalog.ActiveProfile;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _catalog.RestoreActiveProfile(previousProfile.Id);
                await ReloadProfileServicesAsync(_catalog.GetProfileDirectory(previousProfile.Id)).ConfigureAwait(false);
                await RestartRuntimeServicesAsync(
                    hotkeyWasRunning,
                    shortcutWasListening,
                    schedulerWasRunning,
                    textExpansionWasRunning,
                    triggerWasMonitoring).ConfigureAwait(false);
                throw;
            }

            await RestartRuntimeServicesAsync(
                hotkeyWasRunning,
                shortcutWasListening,
                schedulerWasRunning,
                textExpansionWasRunning,
                triggerWasMonitoring).ConfigureAwait(false);

            Log.Information("Switched active profile to {ProfileId}", profile.Id);
        }
        finally
        {
            _ = _gate.Release();
        }

        ProfileChanged?.Invoke(this, new ProfileChangedEventArgs(activeProfile));
    }

    public async Task<ProfileInfo> CreateProfileAsync(string displayName)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try { return await _catalog.CreateProfileAsync(displayName).ConfigureAwait(false); }
        finally { _ = _gate.Release(); }
    }

    public async Task RenameProfileAsync(string profileId, string newDisplayName)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try { await _catalog.RenameProfileAsync(profileId, newDisplayName).ConfigureAwait(false); }
        finally { _ = _gate.Release(); }
    }

    public async Task DeleteProfileAsync(string profileId)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try { await _catalog.DeleteProfileAsync(profileId).ConfigureAwait(false); }
        finally { _ = _gate.Release(); }
    }

    public string GetProfileDirectory(string profileId) => _catalog.GetProfileDirectory(profileId);

    private async Task<bool> StopRuntimeServicesAsync()
    {
        try
        {
            if (_textExpansionService is not null)
            {
                await _textExpansionService.StopExpansionAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { Log.Warning(ex, "Failed to stop text expansion service"); }

        try
        {
            if (_schedulerService is not null)
            {
                await _schedulerService.StopAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { Log.Warning(ex, "Failed to stop scheduler service"); }

        if (_schedulerService is not null && !_schedulerService.Completion.IsCompleted)
        {
            Log.Warning("Profile switch aborted because the scheduler lifetime is still active after shutdown timeout");
            return false;
        }

        try { _triggerService.StopMonitoring(); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { Log.Warning(ex, "Failed to stop trigger service"); }
        try { _shortcutService?.StopShortcuts(); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { Log.Warning(ex, "Failed to stop shortcut service"); }
        try
        {
            if (_hotkeyService is not null)
            {
                await _hotkeyService.StopHotkeyServiceAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { Log.Warning(ex, "Failed to stop hotkey service"); }
        return true;
    }

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

    private async Task RestartRuntimeServicesAsync(bool hotkeyWasRunning, bool shortcutWasListening, bool schedulerWasRunning, bool textExpansionWasRunning, bool triggerWasMonitoring)
    {
        if (hotkeyWasRunning && _hotkeyService is not null)
        {
            try { _hotkeyService.Start(); _hotkeyService.ApplyHotkeys(_hotkeySettings.RecordingHotkey, _hotkeySettings.PlaybackHotkey, _hotkeySettings.PauseHotkey); }
            catch (Exception ex) when (ex is not OutOfMemoryException) { Log.Warning(ex, "Failed to restart hotkey service after profile switch"); }
        }
        if (shortcutWasListening && _shortcutService is not null) { try { _shortcutService.Start(); } catch (Exception ex) when (ex is not OutOfMemoryException) { Log.Warning(ex, "Failed to restart shortcut service after profile switch"); } }
        if (triggerWasMonitoring) { try { _triggerService.Start(); } catch (Exception ex) when (ex is not OutOfMemoryException) { Log.Warning(ex, "Failed to restart trigger service after profile switch"); } }
        if (schedulerWasRunning && _schedulerService is not null) { try { _schedulerService.Start(); } catch (Exception ex) when (ex is not OutOfMemoryException) { Log.Warning(ex, "Failed to restart scheduler after profile switch"); } }
        if (textExpansionWasRunning && _textExpansionService is not null && _settingsService.Current.EnableTextExpansion)
        {
            try { await _textExpansionService.StartAsync(CancellationToken.None).ConfigureAwait(false); }
            catch (Exception ex) when (ex is not OutOfMemoryException) { Log.Warning(ex, "Failed to restart text expansion after profile switch"); }
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) is 0)
        {
            _gate.Dispose();
        }
    }
}
