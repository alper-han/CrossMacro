
using CrossMacro.Infrastructure.Persistence.Settings;
namespace CrossMacro.Infrastructure.Services.Settings;

/// <summary>
/// Service for managing application settings with XDG Base Directory support
/// </summary>
public class SettingsService : ISettingsService, IDisposable
{
    private static readonly TimeSpan SettingsSaveDebounce = TimeSpan.FromSeconds(3);
    private readonly string _globalSettingsFilePath;
    private ProfileSettingsSaveScope _profileSaveScope;
    private readonly Func<AppSettings, PersistedProfileSettings> _profileSnapshotFactory;
    private int _settingsLoaded;
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private readonly DebouncedSaveCoordinator _debouncedSave;
    private int _disposed;
    private readonly Lock _stateGate = new();

    public AppSettings Current { get; private set; }

    public T AccessCurrent<T>(Func<AppSettings, T> access)
    {
        ArgumentNullException.ThrowIfNull(access);
        lock (_stateGate) { return access(Current); }
    }

    public SettingsService() : this(configRootPath: null)
    {
    }

    public SettingsService(string? configRootPath)
        : this(configRootPath, SettingsPersistenceMapper.ToProfile)
    {
    }

    internal SettingsService(string? configRootPath, Func<AppSettings, PersistedProfileSettings> profileSnapshotFactory)
    {
        ArgumentNullException.ThrowIfNull(profileSnapshotFactory);
        _profileSnapshotFactory = profileSnapshotFactory;
        if (string.IsNullOrEmpty(configRootPath))
        {
            configRootPath = ApplicationPathsEnvironment.CaptureCurrent().ConfigDirectory;
        }

        _globalSettingsFilePath = Path.Combine(configRootPath, ConfigFileNames.GlobalSettings);
        _profileSaveScope = new ProfileSettingsSaveScope(Path.Combine(
            configRootPath,
            ConfigFileNames.ProfilesDirectory,
            "default",
            ConfigFileNames.Settings), IsReady: true);

        Current = new AppSettings();
        _debouncedSave = new DebouncedSaveCoordinator(SaveCoreAsync, SettingsSaveDebounce);
    }

    /// <summary>
    /// Try to read log level from settings file before logger is initialized.
    /// This is a static method that doesn't use logging to avoid chicken-and-egg problem.
    /// </summary>
    /// <returns>Log level string or default "Information"</returns>
    public static string TryLoadLogLevelEarly()
    {
        try
        {
            var configDirectory = ApplicationPathsEnvironment.CaptureCurrent().ConfigDirectory;
            var globalSettingsPath = Path.Combine(configDirectory, ConfigFileNames.GlobalSettings);
            if (File.Exists(globalSettingsPath))
            {
                try
                {
                    var globalJson = File.ReadAllText(globalSettingsPath);
                    var globalSettings = JsonSerializer.Deserialize(globalJson, CrossMacroJsonContext.Default.PersistedGlobalSettings);
                    if (!string.IsNullOrWhiteSpace(globalSettings?.LogLevel))
                    {
                        return globalSettings.LogLevel;
                    }
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    // Fall back to the legacy settings file below.
                }
            }

            var settingsPath = Path.Combine(configDirectory, ConfigFileNames.Settings);

            if (!File.Exists(settingsPath))
            {
                return "Information";
            }

            var json = File.ReadAllText(settingsPath);
            var settings = JsonSerializer.Deserialize(json, CrossMacroJsonContext.Default.AppSettings);

            return settings?.LogLevel ?? "Information";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Silently fail and use default - logger isn't initialized yet
            return "Information";
        }
    }

    public async Task<AppSettings> LoadAsync()
    {
        await _saveGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            return await LoadCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            _ = _saveGate.Release();
        }
    }

    public async Task<AppSettings> EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        await _saveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return Volatile.Read(ref _settingsLoaded) is 1
                ? Current
                : await LoadCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            _ = _saveGate.Release();
        }
    }

    private async Task<AppSettings> LoadCoreAsync()
    {
        try
        {
            var globalSettings = await LoadGlobalSettingsAsync().ConfigureAwait(false);
            var profileSettings = await LoadProfileSettingsAsync().ConfigureAwait(false);
            lock (_stateGate)
            {
                Current = SettingsPersistenceMapper.Combine(globalSettings, profileSettings);
                NormalizeSettings(Current);
            }
            Volatile.Write(ref _settingsLoaded, 1);

            Log.Information("Settings loaded from {GlobalPath} and {ProfilePath}", _globalSettingsFilePath, Volatile.Read(ref _profileSaveScope).Path);
            return Current;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "Failed to load settings, using defaults");
            lock (_stateGate)
            {
                Current = new AppSettings();
                NormalizeSettings(Current);
            }
            Volatile.Write(ref _settingsLoaded, 1);
            return Current;
        }
    }

    public AppSettings Load()
    {
        _saveGate.Wait();
        try
        {
            return LoadCore();
        }
        finally
        {
            _ = _saveGate.Release();
        }
    }

    private AppSettings LoadCore()
    {
        try
        {
            var globalSettings = LoadGlobalSettings();
            var profileSettings = LoadProfileSettings();
            lock (_stateGate)
            {
                Current = SettingsPersistenceMapper.Combine(globalSettings, profileSettings);
                NormalizeSettings(Current);
            }
            Volatile.Write(ref _settingsLoaded, 1);

            Log.Information("Settings loaded from {GlobalPath} and {ProfilePath}", _globalSettingsFilePath, Volatile.Read(ref _profileSaveScope).Path);
            return Current;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "Failed to load settings, using defaults");
            lock (_stateGate)
            {
                Current = new AppSettings();
                NormalizeSettings(Current);
            }
            Volatile.Write(ref _settingsLoaded, 1);
            return Current;
        }
    }

    public async Task SaveAsync()
    {
        if (!await _debouncedSave.FlushAsync().ConfigureAwait(false))
        {
            await SaveCoreAsync().ConfigureAwait(false);
        }
    }

    public Task SaveAfterIdleAsync() => _debouncedSave.RequestAsync();

    public Task FlushPendingSaveAsync(CancellationToken cancellationToken = default) =>
        _debouncedSave.FlushAsync(cancellationToken);

    private SaveSnapshot CaptureSaveSnapshot()
    {
        ProfileSettingsSaveScope scope;
        AppSettings snapshot;
        lock (_stateGate)
        {
            scope = Volatile.Read(ref _profileSaveScope);
            snapshot = CrossMacro.Application.Settings.AppSettingsSnapshot.Copy(Current);
        }
        // Mapping and storage operate on an owned copy. A subsequent reload invalidates the
        // captured profile identity without exposing half-applied fields to this save.
        return new SaveSnapshot(_globalSettingsFilePath, scope,
            SettingsPersistenceMapper.ToGlobal(snapshot), _profileSnapshotFactory(snapshot));
    }

    private bool CanSaveProfile(SaveSnapshot snapshot) =>
        snapshot.ProfileScope.IsReady && ReferenceEquals(snapshot.ProfileScope, Volatile.Read(ref _profileSaveScope));

    private async Task SaveCoreAsync()
    {
        var snapshot = CaptureSaveSnapshot();
        await _saveGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            await FileBackedJsonStorage.WriteAsync(
                    snapshot.GlobalPath,
                    snapshot.GlobalSettings,
                    CrossMacroJsonContext.Default.PersistedGlobalSettings,
                    CancellationToken.None)
                .ConfigureAwait(false);

            if (CanSaveProfile(snapshot))
            {
                await FileBackedJsonStorage.WriteAsync(
                        snapshot.ProfileScope.Path,
                        snapshot.ProfileSettings,
                        CrossMacroJsonContext.Default.PersistedProfileSettings,
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }

            Log.Information("Settings save completed for {GlobalPath} and profile scope {ProfilePath}", snapshot.GlobalPath, snapshot.ProfileScope.Path);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "Failed to save settings");
            throw;
        }
        finally
        {
            _ = _saveGate.Release();
        }
    }

    public void Save()
    {
        if (!_debouncedSave.FlushAsync().GetAwaiter().GetResult())
        {
            var snapshot = CaptureSaveSnapshot();
            _saveGate.Wait();
            try
            {
                FileBackedJsonStorage.Write(
                    _globalSettingsFilePath,
                    snapshot.GlobalSettings,
                    CrossMacroJsonContext.Default.PersistedGlobalSettings);

                if (CanSaveProfile(snapshot))
                {
                    FileBackedJsonStorage.Write(
                        snapshot.ProfileScope.Path,
                        snapshot.ProfileSettings,
                        CrossMacroJsonContext.Default.PersistedProfileSettings);
                }

                Log.Information("Settings save completed for {GlobalPath} and profile scope {ProfilePath}", snapshot.GlobalPath, snapshot.ProfileScope.Path);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.LogError(ex, "Failed to save settings");
                throw;
            }
            finally
            {
                _ = _saveGate.Release();
            }
        }
    }

    public async Task ReloadAsync(string profileConfigDirectory)
    {
        var profilePath = Path.Combine(profileConfigDirectory, ConfigFileNames.Settings);
        _ = await _debouncedSave.FlushAsync().ConfigureAwait(false);
        await _saveGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        Volatile.Write(ref _profileSaveScope, new ProfileSettingsSaveScope(profilePath, IsReady: false));
        try
        {
            if (Volatile.Read(ref _settingsLoaded) is 0)
            {
                var globalSettings = await LoadGlobalSettingsAsync().ConfigureAwait(false);
                var profileSettings = await LoadProfileSettingsAsync().ConfigureAwait(false);
                lock (_stateGate)
                {
                    Current = SettingsPersistenceMapper.Combine(globalSettings, profileSettings);
                    NormalizeSettings(Current);
                }
                Volatile.Write(ref _settingsLoaded, 1);

                Log.Information("Settings loaded from {GlobalPath} and {ProfilePath}", _globalSettingsFilePath, Volatile.Read(ref _profileSaveScope).Path);
            }
            else
            {
                var profileSettings = await LoadProfileSettingsAsync().ConfigureAwait(false);
                lock (_stateGate)
                {
                    SettingsPersistenceMapper.ApplyProfile(Current, profileSettings);
                    NormalizeSettings(Current);
                }

                Log.Information("Profile settings reloaded from {ProfilePath}", Volatile.Read(ref _profileSaveScope).Path);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "Failed to reload profile settings, using defaults");
            lock (_stateGate)
            {
                SettingsPersistenceMapper.ApplyProfile(Current, new PersistedProfileSettings());
                NormalizeSettings(Current);
            }
            Volatile.Write(ref _settingsLoaded, 1);
        }
        finally
        {
            Volatile.Write(ref _profileSaveScope, new ProfileSettingsSaveScope(profilePath, IsReady: true));
            _ = _saveGate.Release();
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing && Interlocked.Exchange(ref _disposed, 1) is 0)
        {
            _debouncedSave.Dispose();
            _saveGate.Dispose();
        }
    }

    private async Task<PersistedGlobalSettings> LoadGlobalSettingsAsync()
    {
        if (!File.Exists(_globalSettingsFilePath))
        {
            Log.Information("Global settings file not found, using defaults");
            var globalSettings = new PersistedGlobalSettings();
            await FileBackedJsonStorage.WriteAsync(
                    _globalSettingsFilePath,
                    globalSettings,
                    CrossMacroJsonContext.Default.PersistedGlobalSettings,
                    CancellationToken.None)
                .ConfigureAwait(false);
            return globalSettings;
        }

        return await FileBackedJsonStorage.ReadAsync(_globalSettingsFilePath, CrossMacroJsonContext.Default.PersistedGlobalSettings)
                .ConfigureAwait(false)
            ?? new PersistedGlobalSettings();
    }

    private PersistedGlobalSettings LoadGlobalSettings()
    {
        if (!File.Exists(_globalSettingsFilePath))
        {
            Log.Information("Global settings file not found, using defaults");
            var globalSettings = new PersistedGlobalSettings();
            FileBackedJsonStorage.Write(
                _globalSettingsFilePath,
                globalSettings,
                CrossMacroJsonContext.Default.PersistedGlobalSettings);
            return globalSettings;
        }

        return FileBackedJsonStorage.Read(_globalSettingsFilePath, CrossMacroJsonContext.Default.PersistedGlobalSettings)
            ?? new PersistedGlobalSettings();
    }

    private async Task<PersistedProfileSettings> LoadProfileSettingsAsync()
    {
        if (!File.Exists(Volatile.Read(ref _profileSaveScope).Path))
        {
            Log.Information("Profile settings file not found, using defaults");
            var profileSettings = new PersistedProfileSettings();
            await FileBackedJsonStorage.WriteAsync(
                    Volatile.Read(ref _profileSaveScope).Path,
                    profileSettings,
                    CrossMacroJsonContext.Default.PersistedProfileSettings,
                    CancellationToken.None)
                .ConfigureAwait(false);
            return profileSettings;
        }

        return await FileBackedJsonStorage.ReadAsync(Volatile.Read(ref _profileSaveScope).Path, CrossMacroJsonContext.Default.PersistedProfileSettings)
                .ConfigureAwait(false)
            ?? new PersistedProfileSettings();
    }

    private PersistedProfileSettings LoadProfileSettings()
    {
        if (!File.Exists(Volatile.Read(ref _profileSaveScope).Path))
        {
            Log.Information("Profile settings file not found, using defaults");
            var profileSettings = new PersistedProfileSettings();
            FileBackedJsonStorage.Write(
                Volatile.Read(ref _profileSaveScope).Path,
                profileSettings,
                CrossMacroJsonContext.Default.PersistedProfileSettings);
            return profileSettings;
        }

        return FileBackedJsonStorage.Read(Volatile.Read(ref _profileSaveScope).Path, CrossMacroJsonContext.Default.PersistedProfileSettings)
            ?? new PersistedProfileSettings();
    }

    private static void NormalizeSettings(AppSettings settings)
    {
        settings.Normalize();
    }

    private sealed record SaveSnapshot(
        string GlobalPath,
        ProfileSettingsSaveScope ProfileScope,
        PersistedGlobalSettings GlobalSettings,
        PersistedProfileSettings ProfileSettings);
}
