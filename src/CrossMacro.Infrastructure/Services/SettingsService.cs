using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using Serilog;
using CrossMacro.Core;
using CrossMacro.Infrastructure.Serialization;
using CrossMacro.Infrastructure.Helpers;

namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Service for managing application settings with XDG Base Directory support
/// </summary>
public class SettingsService : ISettingsService
{
    private const string SettingsFileName = ConfigFileNames.Settings;
    private readonly string _configDirectory;
    private readonly string _settingsFilePath;
    private AppSettings _currentSettings;

    public AppSettings Current => _currentSettings;

    public SettingsService() : this(null)
    {
    }

    public SettingsService(string? configRootPath)
    {
        if (string.IsNullOrEmpty(configRootPath))
        {
            _configDirectory = PathHelper.GetConfigDirectory();
        }
        else
        {
            _configDirectory = configRootPath;
        }

        _settingsFilePath = Path.Combine(_configDirectory, SettingsFileName);
        
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

            var json = await File.ReadAllTextAsync(_settingsFilePath);
            _currentSettings = JsonSerializer.Deserialize(json, CrossMacroJsonContext.Default.AppSettings) ?? new AppSettings();
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

            var json = File.ReadAllText(_settingsFilePath);
            _currentSettings = JsonSerializer.Deserialize(json, CrossMacroJsonContext.Default.AppSettings) ?? new AppSettings();
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
        try
        {
            // Ensure config directory exists
            Directory.CreateDirectory(_configDirectory);

            var json = JsonSerializer.Serialize(_currentSettings, CrossMacroJsonContext.Default.AppSettings);
            await File.WriteAllTextAsync(_settingsFilePath, json);
            
            Log.Information("Settings saved to {Path}", _settingsFilePath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save settings");
            throw;
        }
    }

    public void Save()
    {
        try
        {
            // Ensure config directory exists
            Directory.CreateDirectory(_configDirectory);

            var json = JsonSerializer.Serialize(_currentSettings, CrossMacroJsonContext.Default.AppSettings);
            File.WriteAllText(_settingsFilePath, json);
            
            Log.Information("Settings saved to {Path}", _settingsFilePath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save settings");
            throw;
        }
    }

    private static void NormalizeSettings(AppSettings settings)
    {
        settings.PlaybackSpeed = PlaybackOptions.NormalizeSpeedMultiplier(settings.PlaybackSpeed);
    }
}
