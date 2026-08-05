
using CrossMacro.Infrastructure.Persistence.Settings;

namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Service for managing application settings with XDG Base Directory support
/// </summary>
public class SettingsService : ISettingsService, IDisposable
{
    private readonly string _globalSettingsFilePath;
    private string _profileSettingsFilePath;
    private int _profileGeneration;
    private int _settingsLoaded;
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private int _disposed;

    public AppSettings Current { get; private set; }

    public SettingsService() : this(configRootPath: null)
    {
    }

    public SettingsService(string? configRootPath)
    {
        if (string.IsNullOrEmpty(configRootPath))
        {
            configRootPath = PathHelper.GetConfigDirectory();
        }

        _globalSettingsFilePath = Path.Combine(configRootPath, ConfigFileNames.GlobalSettings);
        _profileSettingsFilePath = Path.Combine(
            configRootPath,
            ConfigFileNames.ProfilesDirectory,
            "default",
            ConfigFileNames.Settings);

        Current = new AppSettings();
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
            var configDirectory = PathHelper.GetConfigDirectory();
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
        try
        {
            var globalSettings = await LoadGlobalSettingsAsync().ConfigureAwait(false);
            var profileSettings = await LoadProfileSettingsAsync().ConfigureAwait(false);
            Current = SettingsPersistenceMapper.Combine(globalSettings, profileSettings);
            NormalizeSettings(Current);
            Volatile.Write(ref _settingsLoaded, 1);

            Log.Information("Settings loaded from {GlobalPath} and {ProfilePath}", _globalSettingsFilePath, _profileSettingsFilePath);
            return Current;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "Failed to load settings, using defaults");
            Current = new AppSettings();
            NormalizeSettings(Current);
            Volatile.Write(ref _settingsLoaded, 1);
            return Current;
        }
    }

    public AppSettings Load()
    {
        try
        {
            var globalSettings = LoadGlobalSettings();
            var profileSettings = LoadProfileSettings();
            Current = SettingsPersistenceMapper.Combine(globalSettings, profileSettings);
            NormalizeSettings(Current);
            Volatile.Write(ref _settingsLoaded, 1);

            Log.Information("Settings loaded from {GlobalPath} and {ProfilePath}", _globalSettingsFilePath, _profileSettingsFilePath);
            return Current;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "Failed to load settings, using defaults");
            Current = new AppSettings();
            NormalizeSettings(Current);
            Volatile.Write(ref _settingsLoaded, 1);
            return Current;
        }
    }

    public async Task SaveAsync()
    {
        var snapshot = new SaveSnapshot(
            _globalSettingsFilePath,
            _profileSettingsFilePath,
            SettingsPersistenceMapper.ToGlobal(Current),
            SettingsPersistenceMapper.ToProfile(Current),
            _profileGeneration);

        await _saveGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await FileBackedJsonStorage.WriteAsync(
                    snapshot.GlobalPath,
                    snapshot.GlobalSettings,
                    CrossMacroJsonContext.Default.PersistedGlobalSettings)
                .ConfigureAwait(false);

            if (snapshot.ProfileGeneration == _profileGeneration && snapshot.ProfileGeneration % 2 is 0)
            {
                await FileBackedJsonStorage.WriteAsync(
                        snapshot.ProfilePath,
                        snapshot.ProfileSettings,
                        CrossMacroJsonContext.Default.PersistedProfileSettings)
                    .ConfigureAwait(false);
            }

            Log.Information("Settings saved to {GlobalPath} and {ProfilePath}", snapshot.GlobalPath, snapshot.ProfilePath);
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
        _saveGate.Wait();
        try
        {
            FileBackedJsonStorage.Write(
                _globalSettingsFilePath,
                SettingsPersistenceMapper.ToGlobal(Current),
                CrossMacroJsonContext.Default.PersistedGlobalSettings);

            FileBackedJsonStorage.Write(
                _profileSettingsFilePath,
                SettingsPersistenceMapper.ToProfile(Current),
                CrossMacroJsonContext.Default.PersistedProfileSettings);

            Log.Information("Settings saved to {GlobalPath} and {ProfilePath}", _globalSettingsFilePath, _profileSettingsFilePath);
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
        await _saveGate.WaitAsync().ConfigureAwait(false);
        _profileGeneration++;
        try
        {
            _profileSettingsFilePath = Path.Combine(profileConfigDirectory, ConfigFileNames.Settings);
            if (Volatile.Read(ref _settingsLoaded) is 0)
            {
                var globalSettings = await LoadGlobalSettingsAsync().ConfigureAwait(false);
                var profileSettings = await LoadProfileSettingsAsync().ConfigureAwait(false);
                Current = SettingsPersistenceMapper.Combine(globalSettings, profileSettings);
                NormalizeSettings(Current);
                Volatile.Write(ref _settingsLoaded, 1);

                Log.Information("Settings loaded from {GlobalPath} and {ProfilePath}", _globalSettingsFilePath, _profileSettingsFilePath);
            }
            else
            {
                var profileSettings = await LoadProfileSettingsAsync().ConfigureAwait(false);
                SettingsPersistenceMapper.ApplyProfile(Current, profileSettings);
                NormalizeSettings(Current);

                Log.Information("Profile settings reloaded from {ProfilePath}", _profileSettingsFilePath);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "Failed to reload profile settings, using defaults");
            SettingsPersistenceMapper.ApplyProfile(Current, new PersistedProfileSettings());
            NormalizeSettings(Current);
            Volatile.Write(ref _settingsLoaded, 1);
        }
        finally
        {
            _profileGeneration++;
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
        if (!File.Exists(_profileSettingsFilePath))
        {
            Log.Information("Profile settings file not found, using defaults");
            var profileSettings = new PersistedProfileSettings();
            await FileBackedJsonStorage.WriteAsync(
                    _profileSettingsFilePath,
                    profileSettings,
                    CrossMacroJsonContext.Default.PersistedProfileSettings,
                    CancellationToken.None)
                .ConfigureAwait(false);
            return profileSettings;
        }

        return await FileBackedJsonStorage.ReadAsync(_profileSettingsFilePath, CrossMacroJsonContext.Default.PersistedProfileSettings)
                .ConfigureAwait(false)
            ?? new PersistedProfileSettings();
    }

    private PersistedProfileSettings LoadProfileSettings()
    {
        if (!File.Exists(_profileSettingsFilePath))
        {
            Log.Information("Profile settings file not found, using defaults");
            var profileSettings = new PersistedProfileSettings();
            FileBackedJsonStorage.Write(
                _profileSettingsFilePath,
                profileSettings,
                CrossMacroJsonContext.Default.PersistedProfileSettings);
            return profileSettings;
        }

        return FileBackedJsonStorage.Read(_profileSettingsFilePath, CrossMacroJsonContext.Default.PersistedProfileSettings)
            ?? new PersistedProfileSettings();
    }

    private static void NormalizeSettings(AppSettings settings)
    {
        settings.Normalize();
    }

    private sealed record SaveSnapshot(
        string GlobalPath,
        string ProfilePath,
        PersistedGlobalSettings GlobalSettings,
        PersistedProfileSettings ProfileSettings,
        int ProfileGeneration);
}
