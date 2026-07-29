
namespace CrossMacro.Infrastructure.Services;

public class HotkeyConfigurationService : IHotkeyConfigurationService
{
    private readonly Lock _pathLock = new();
    private string _configPath;

    public HotkeyConfigurationService() : this(configRootPath: null)
    {
    }

    public HotkeyConfigurationService(string? configRootPath)
    {
        if (string.IsNullOrEmpty(configRootPath))
        {
            configRootPath = PathHelper.GetConfigDirectory();
        }

        if (!Directory.Exists(configRootPath))
        {
            _ = Directory.CreateDirectory(configRootPath);
        }

        _configPath = Path.Combine(configRootPath, ConfigFileNames.Hotkeys);
    }

    public HotkeySettings Load()
    {
        string configPath;
        lock (_pathLock)
        {
            configPath = _configPath;
        }

        try
        {
            if (File.Exists(configPath))
            {
                var settings = FileBackedJsonStorage.Read(configPath, CrossMacroJsonContext.Default.HotkeySettings);
                if (settings is not null)
                {
                    Log.Information("Loaded hotkey configuration from {Path}", configPath);
                    return settings;
                }
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "Failed to load hotkey configuration from {Path}", configPath);
        }

        Log.Information("Using default hotkey configuration");
        return new HotkeySettings();
    }

    public async Task<HotkeySettings> LoadAsync()
    {
        string configPath;
        lock (_pathLock)
        {
            configPath = _configPath;
        }

        try
        {
            if (!File.Exists(configPath))
            {
                Log.Information("Using default hotkey configuration");
                return new HotkeySettings();
            }

            var settings = await FileBackedJsonStorage.ReadAsync(configPath, CrossMacroJsonContext.Default.HotkeySettings)
                .ConfigureAwait(false);
            if (settings is not null)
            {
                Log.Information("Loaded hotkey configuration from {Path}", configPath);
                return settings;
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "Failed to load hotkey configuration from {Path}", configPath);
        }

        Log.Information("Using default hotkey configuration");
        return new HotkeySettings();
    }

    public Task ReloadAsync(string profileConfigDirectory)
    {
        lock (_pathLock)
        {
            _configPath = Path.Combine(profileConfigDirectory, ConfigFileNames.Hotkeys);
        }

        return LoadAsync();
    }

    public HotkeyConfigurationSaveRequest CaptureSaveRequest(HotkeySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        string configPath;
        lock (_pathLock)
        {
            configPath = _configPath;
        }

        return new HotkeyConfigurationSaveRequest(
            configPath,
            settings.RecordingHotkey,
            settings.PlaybackHotkey,
            settings.PauseHotkey);
    }

    public void Save(HotkeySettings settings)
    {
        _ = TrySave(CaptureSaveRequest(settings));
    }

    public bool TrySave(HotkeyConfigurationSaveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            FileBackedJsonStorage.Write(request.ConfigPath, new HotkeySettings
            {
                RecordingHotkey = request.RecordingHotkey,
                PlaybackHotkey = request.PlaybackHotkey,
                PauseHotkey = request.PauseHotkey,
            }, CrossMacroJsonContext.Default.HotkeySettings);
            Log.Information("Saved hotkey configuration to {Path}", request.ConfigPath);
            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "Failed to save hotkey configuration to {Path}", request.ConfigPath);
            return false;
        }
    }

    public async Task<bool> TrySaveAsync(HotkeyConfigurationSaveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            await FileBackedJsonStorage.WriteAsync(request.ConfigPath, new HotkeySettings
            {
                RecordingHotkey = request.RecordingHotkey,
                PlaybackHotkey = request.PlaybackHotkey,
                PauseHotkey = request.PauseHotkey,
            }, CrossMacroJsonContext.Default.HotkeySettings).ConfigureAwait(false);
            Log.Information("Saved hotkey configuration to {Path}", request.ConfigPath);
            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "Failed to save hotkey configuration to {Path}", request.ConfigPath);
            return false;
        }
    }
}
