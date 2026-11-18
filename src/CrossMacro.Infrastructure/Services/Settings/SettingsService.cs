
using CrossMacro.Infrastructure.Persistence.Settings;
namespace CrossMacro.Infrastructure.Services.Settings;

/// <summary>
/// Persists application settings as a global file plus one file per profile.
/// A file that cannot be read is never overwritten by defaults: malformed files are renamed aside,
/// and unreadable files disable saving that file until a later load succeeds.
/// </summary>
public sealed class SettingsService : ISettingsService, IAsyncDisposable
{
    private const string DefaultLogLevel = "Information";

    private static readonly TimeSpan SettingsSaveDebounce = TimeSpan.FromSeconds(3);
    private readonly string _globalSettingsFilePath;
    private ProfileSettingsSaveScope _profileSaveScope;
    private readonly Func<AppSettings, PersistedProfileSettings> _profileSnapshotFactory;
    private int _settingsLoaded;
    private bool _globalSettingsWritable = true;
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

    public SettingsService(string configRootPath)
        : this(configRootPath, SettingsPersistenceMapper.ToProfile)
    {
    }

    internal SettingsService(string configRootPath, Func<AppSettings, PersistedProfileSettings> profileSnapshotFactory)
    {
        ArgumentException.ThrowIfNullOrEmpty(configRootPath);
        ArgumentNullException.ThrowIfNull(profileSnapshotFactory);
        _profileSnapshotFactory = profileSnapshotFactory;

        _globalSettingsFilePath = Path.Combine(configRootPath, ConfigFileNames.GlobalSettings);
        _profileSaveScope = new ProfileSettingsSaveScope(Path.Combine(
            configRootPath,
            ConfigFileNames.ProfilesDirectory,
            ConfigFileNames.DefaultProfileId,
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
            var globalSettingsPath = ApplicationPathsEnvironment.CaptureCurrent().GetConfigFilePath(ConfigFileNames.GlobalSettings);
            if (!File.Exists(globalSettingsPath))
            {
                return DefaultLogLevel;
            }

            var globalJson = File.ReadAllText(globalSettingsPath);
            var globalSettings = JsonSerializer.Deserialize(globalJson, CrossMacroJsonContext.Default.PersistedGlobalSettings);
            return string.IsNullOrWhiteSpace(globalSettings?.LogLevel) ? DefaultLogLevel : globalSettings.LogLevel;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The logger is not initialized yet; start with the default level.
            return DefaultLogLevel;
        }
    }

    public async Task<AppSettings> LoadAsync()
    {
        await _saveGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            return await LoadCoreAsync(Volatile.Read(ref _profileSaveScope).Path).ConfigureAwait(false);
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
                : await LoadCoreAsync(Volatile.Read(ref _profileSaveScope).Path).ConfigureAwait(false);
        }
        finally
        {
            _ = _saveGate.Release();
        }
    }

    private async Task<AppSettings> LoadCoreAsync(string profilePath)
    {
        var global = await LoadFileAsync(
            _globalSettingsFilePath,
            CrossMacroJsonContext.Default.PersistedGlobalSettings,
            static () => new PersistedGlobalSettings()).ConfigureAwait(false);
        var profile = await LoadFileAsync(
            profilePath,
            CrossMacroJsonContext.Default.PersistedProfileSettings,
            static () => new PersistedProfileSettings()).ConfigureAwait(false);

        lock (_stateGate)
        {
            Current = SettingsPersistenceMapper.Combine(global.Value, profile.Value);
            Current.Normalize();
        }

        Volatile.Write(ref _globalSettingsWritable, global.Writable);
        Volatile.Write(ref _profileSaveScope, new ProfileSettingsSaveScope(profilePath, profile.Writable));
        Volatile.Write(ref _settingsLoaded, 1);

        Log.Information("Settings loaded from {GlobalPath} and {ProfilePath}", _globalSettingsFilePath, profilePath);
        return Current;
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
            if (Volatile.Read(ref _globalSettingsWritable))
            {
                await FileBackedJsonStorage.WriteAsync(
                        snapshot.GlobalPath,
                        snapshot.GlobalSettings,
                        CrossMacroJsonContext.Default.PersistedGlobalSettings,
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }
            else
            {
                Log.Warning("Global settings were not saved because {Path} could not be read earlier", snapshot.GlobalPath);
            }

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
                _ = await LoadCoreAsync(profilePath).ConfigureAwait(false);
                return;
            }

            var profile = await LoadFileAsync(
                profilePath,
                CrossMacroJsonContext.Default.PersistedProfileSettings,
                static () => new PersistedProfileSettings()).ConfigureAwait(false);
            lock (_stateGate)
            {
                SettingsPersistenceMapper.ApplyProfile(Current, profile.Value);
                Current.Normalize();
            }

            Volatile.Write(ref _profileSaveScope, new ProfileSettingsSaveScope(profilePath, profile.Writable));
            Log.Information("Profile settings reloaded from {ProfilePath}", profilePath);
        }
        finally
        {
            _ = _saveGate.Release();
        }
    }

    /// <summary>Writes any pending save without blocking the caller, then releases the save gate.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) is 0)
        {
            await _debouncedSave.DisposeAsync().ConfigureAwait(false);
            _saveGate.Dispose();
        }
    }

    /// <summary>
    /// Reads one settings file. A missing file yields defaults. A malformed file is moved aside and yields
    /// defaults. A file that cannot be read at all yields defaults that must not be saved over it.
    /// </summary>
    private static async Task<LoadedFile<T>> LoadFileAsync<T>(string path, JsonTypeInfo<T> typeInfo, Func<T> createDefault)
        where T : class
    {
        if (!File.Exists(path))
        {
            Log.Information("Settings file {Path} not found, using defaults", path);
            return new LoadedFile<T>(createDefault(), Writable: true);
        }

        try
        {
            var value = await FileBackedJsonStorage.ReadAsync(path, typeInfo, CancellationToken.None).ConfigureAwait(false);
            return new LoadedFile<T>(value ?? createDefault(), Writable: true);
        }
        catch (JsonException ex)
        {
            return new LoadedFile<T>(createDefault(), Writable: TryMoveAsideCorruptFile(path, ex));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "Failed to read settings file {Path}; it will not be overwritten until it loads successfully", path);
            return new LoadedFile<T>(createDefault(), Writable: false);
        }
    }

    private static bool TryMoveAsideCorruptFile(string path, JsonException parseError)
    {
        try
        {
            var backupPath = FileBackedJsonStorage.MoveAsideCorruptFile(path);
            Log.LogError(parseError, "Settings file {Path} is malformed; it was kept as {BackupPath} and defaults are used", path, backupPath);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.LogError(
                ex,
                "Settings file {Path} is malformed and could not be moved aside; it will not be overwritten until it loads successfully",
                path);
            return false;
        }
    }

    private readonly record struct LoadedFile<T>(T Value, bool Writable);

    private sealed record SaveSnapshot(
        string GlobalPath,
        ProfileSettingsSaveScope ProfileScope,
        PersistedGlobalSettings GlobalSettings,
        PersistedProfileSettings ProfileSettings);
}
