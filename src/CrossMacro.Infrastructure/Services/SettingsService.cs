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
    private const string SettingsFileName = ConfigFileNames.Settings;
    private readonly string _settingsFilePath;
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

        _settingsFilePath = Path.Combine(configRootPath, SettingsFileName);
        
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
            var settingsPath = Path.Combine(PathHelper.GetConfigDirectory(), ConfigFileNames.Settings);
            
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
            if (!File.Exists(_settingsFilePath))
            {
                Log.Information("Settings file not found, using defaults");
                _currentSettings = new AppSettings();
                NormalizeSettings(_currentSettings);
                await SaveAsync();
                return _currentSettings;
            }

            _currentSettings = await FileBackedJsonStorage.ReadAsync(_settingsFilePath, CrossMacroJsonContext.Default.AppSettings).ConfigureAwait(false)
                ?? new AppSettings();
            NormalizeSettings(_currentSettings);
            
            Log.Information("Settings loaded from {Path}", _settingsFilePath);
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
            if (!File.Exists(_settingsFilePath))
            {
                Log.Information("Settings file not found, using defaults");
                _currentSettings = new AppSettings();
                NormalizeSettings(_currentSettings);
                Save(); // Use synchronous save to avoid deadlock
                return _currentSettings;
            }

            _currentSettings = FileBackedJsonStorage.Read(_settingsFilePath, CrossMacroJsonContext.Default.AppSettings)
                ?? new AppSettings();
            NormalizeSettings(_currentSettings);
            
            Log.Information("Settings loaded from {Path}", _settingsFilePath);
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
            await FileBackedJsonStorage.WriteAsync(_settingsFilePath, _currentSettings, CrossMacroJsonContext.Default.AppSettings)
                .ConfigureAwait(false);
            
            Log.Information("Settings saved to {Path}", _settingsFilePath);
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
            FileBackedJsonStorage.Write(_settingsFilePath, _currentSettings, CrossMacroJsonContext.Default.AppSettings);
            
            Log.Information("Settings saved to {Path}", _settingsFilePath);
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
