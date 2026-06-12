using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core;
using CrossMacro.Core.Logging;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Serialization;
using CrossMacro.Infrastructure.Helpers;

namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Service for managing application settings with XDG Base Directory support
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly string _configRootPath;
    private readonly string _globalSettingsFilePath;
    private string _profileSettingsFilePath;
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private AppSettings _currentSettings;

    public AppSettings Current => _currentSettings;

    public SettingsService() : this(null)
    {
    }

    public SettingsService(string? configRootPath)
    {
        if (string.IsNullOrEmpty(configRootPath))
        {
            configRootPath = PathHelper.GetConfigDirectory();
        }

        _configRootPath = configRootPath;
        _globalSettingsFilePath = Path.Combine(_configRootPath, ConfigFileNames.GlobalSettings);
        _profileSettingsFilePath = Path.Combine(
            _configRootPath,
            ConfigFileNames.ProfilesDirectory,
            "default",
            ConfigFileNames.Settings);
        
        _currentSettings = new AppSettings();
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
                    var globalSettings = JsonSerializer.Deserialize(globalJson, CrossMacroJsonContext.Default.GlobalSettings);
                    if (!string.IsNullOrWhiteSpace(globalSettings?.LogLevel))
                    {
                        return globalSettings.LogLevel;
                    }
                }
                catch
                {
                    // Fall back to the legacy settings file below.
                }
            }

            var settingsPath = Path.Combine(configDirectory, ConfigFileNames.Settings);

            if (!File.Exists(settingsPath))
                return "Information";
            
            var json = File.ReadAllText(settingsPath);
            var settings = JsonSerializer.Deserialize(json, CrossMacroJsonContext.Default.AppSettings);
            
            return settings?.LogLevel ?? "Information";
        }
        catch
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
            _currentSettings = SettingsMapper.Combine(globalSettings, profileSettings);
            NormalizeSettings(_currentSettings);

            Log.Information("Settings loaded from {GlobalPath} and {ProfilePath}", _globalSettingsFilePath, _profileSettingsFilePath);
            return _currentSettings;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load settings, using defaults");
            _currentSettings = new AppSettings();
            NormalizeSettings(_currentSettings);
            return _currentSettings;
        }
    }

    public AppSettings Load()
    {
        try
        {
            var globalSettings = LoadGlobalSettings();
            var profileSettings = LoadProfileSettings();
            _currentSettings = SettingsMapper.Combine(globalSettings, profileSettings);
            NormalizeSettings(_currentSettings);

            Log.Information("Settings loaded from {GlobalPath} and {ProfilePath}", _globalSettingsFilePath, _profileSettingsFilePath);
            return _currentSettings;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load settings, using defaults");
            _currentSettings = new AppSettings();
            NormalizeSettings(_currentSettings);
            return _currentSettings;
        }
    }

    public async Task SaveAsync()
    {
        await _saveGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await FileBackedJsonStorage.WriteAsync(
                    _globalSettingsFilePath,
                    SettingsMapper.ToGlobal(_currentSettings),
                    CrossMacroJsonContext.Default.GlobalSettings)
                .ConfigureAwait(false);

            await FileBackedJsonStorage.WriteAsync(
                    _profileSettingsFilePath,
                    SettingsMapper.ToProfile(_currentSettings),
                    CrossMacroJsonContext.Default.ProfileSettings)
                .ConfigureAwait(false);
            
            Log.Information("Settings saved to {GlobalPath} and {ProfilePath}", _globalSettingsFilePath, _profileSettingsFilePath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save settings");
            throw;
        }
        finally
        {
            _saveGate.Release();
        }
    }

    public void Save()
    {
        _saveGate.Wait();
        try
        {
            FileBackedJsonStorage.Write(
                _globalSettingsFilePath,
                SettingsMapper.ToGlobal(_currentSettings),
                CrossMacroJsonContext.Default.GlobalSettings);

            FileBackedJsonStorage.Write(
                _profileSettingsFilePath,
                SettingsMapper.ToProfile(_currentSettings),
                CrossMacroJsonContext.Default.ProfileSettings);
            
            Log.Information("Settings saved to {GlobalPath} and {ProfilePath}", _globalSettingsFilePath, _profileSettingsFilePath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save settings");
            throw;
        }
        finally
        {
            _saveGate.Release();
        }
    }

    public async Task ReloadAsync(string profileConfigDirectory)
    {
        await _saveGate.WaitAsync().ConfigureAwait(false);
        try
        {
            _profileSettingsFilePath = Path.Combine(profileConfigDirectory, ConfigFileNames.Settings);
            var profileSettings = await LoadProfileSettingsAsync().ConfigureAwait(false);
            SettingsMapper.ApplyProfile(_currentSettings, profileSettings);
            NormalizeSettings(_currentSettings);

            Log.Information("Profile settings reloaded from {ProfilePath}", _profileSettingsFilePath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to reload profile settings, using defaults");
            SettingsMapper.ApplyProfile(_currentSettings, new ProfileSettings());
            NormalizeSettings(_currentSettings);
        }
        finally
        {
            _saveGate.Release();
        }
    }

    private async Task<GlobalSettings> LoadGlobalSettingsAsync()
    {
        if (!File.Exists(_globalSettingsFilePath))
        {
            Log.Information("Global settings file not found, using defaults");
            var globalSettings = new GlobalSettings();
            await FileBackedJsonStorage.WriteAsync(
                    _globalSettingsFilePath,
                    globalSettings,
                    CrossMacroJsonContext.Default.GlobalSettings)
                .ConfigureAwait(false);
            return globalSettings;
        }

        return await FileBackedJsonStorage.ReadAsync(_globalSettingsFilePath, CrossMacroJsonContext.Default.GlobalSettings)
                .ConfigureAwait(false)
            ?? new GlobalSettings();
    }

    private GlobalSettings LoadGlobalSettings()
    {
        if (!File.Exists(_globalSettingsFilePath))
        {
            Log.Information("Global settings file not found, using defaults");
            var globalSettings = new GlobalSettings();
            FileBackedJsonStorage.Write(
                _globalSettingsFilePath,
                globalSettings,
                CrossMacroJsonContext.Default.GlobalSettings);
            return globalSettings;
        }

        return FileBackedJsonStorage.Read(_globalSettingsFilePath, CrossMacroJsonContext.Default.GlobalSettings)
            ?? new GlobalSettings();
    }

    private async Task<ProfileSettings> LoadProfileSettingsAsync()
    {
        if (!File.Exists(_profileSettingsFilePath))
        {
            Log.Information("Profile settings file not found, using defaults");
            var profileSettings = new ProfileSettings();
            await FileBackedJsonStorage.WriteAsync(
                    _profileSettingsFilePath,
                    profileSettings,
                    CrossMacroJsonContext.Default.ProfileSettings)
                .ConfigureAwait(false);
            return profileSettings;
        }

        return await FileBackedJsonStorage.ReadAsync(_profileSettingsFilePath, CrossMacroJsonContext.Default.ProfileSettings)
                .ConfigureAwait(false)
            ?? new ProfileSettings();
    }

    private ProfileSettings LoadProfileSettings()
    {
        if (!File.Exists(_profileSettingsFilePath))
        {
            Log.Information("Profile settings file not found, using defaults");
            var profileSettings = new ProfileSettings();
            FileBackedJsonStorage.Write(
                _profileSettingsFilePath,
                profileSettings,
                CrossMacroJsonContext.Default.ProfileSettings);
            return profileSettings;
        }

        return FileBackedJsonStorage.Read(_profileSettingsFilePath, CrossMacroJsonContext.Default.ProfileSettings)
            ?? new ProfileSettings();
    }

    private static void NormalizeSettings(AppSettings settings)
    {
        settings.PlaybackSpeed = PlaybackOptions.NormalizeSpeedMultiplier(settings.PlaybackSpeed);
        settings.LoopDelayMs = PlaybackOptions.NormalizeDelayMs(settings.LoopDelayMs);

        var (loopDelayMinMs, loopDelayMaxMs) = PlaybackOptions.NormalizeDelayRange(
            settings.LoopDelayMinMs,
            settings.LoopDelayMaxMs);
        settings.LoopDelayMinMs = loopDelayMinMs;
        settings.LoopDelayMaxMs = loopDelayMaxMs;
    }
}
