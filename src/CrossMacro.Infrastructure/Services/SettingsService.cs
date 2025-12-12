using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using Serilog;
using CrossMacro.Core;

namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Service for managing application settings with XDG Base Directory support
/// </summary>
public class SettingsService : ISettingsService
{
    private const string SettingsFileName = "settings.json";
    private readonly string _configDirectory;
    private readonly string _settingsFilePath;
    private AppSettings _currentSettings;

    public AppSettings Current => _currentSettings;

    public SettingsService()
    {
        // Follow XDG Base Directory specification
        var xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var configHome = string.IsNullOrEmpty(xdgConfigHome)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config")
            : xdgConfigHome;

        _configDirectory = Path.Combine(configHome, AppConstants.AppIdentifier);
        _settingsFilePath = Path.Combine(_configDirectory, SettingsFileName);
        
        _currentSettings = new AppSettings();
    }

    public async Task<AppSettings> LoadAsync()
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                Log.Information("Settings file not found, using defaults");
                _currentSettings = new AppSettings();
                await SaveAsync();
                return _currentSettings;
            }

            var json = await File.ReadAllTextAsync(_settingsFilePath);
            _currentSettings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            
            Log.Information("Settings loaded from {Path}", _settingsFilePath);
            return _currentSettings;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load settings, using defaults");
            _currentSettings = new AppSettings();
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
                Save(); // Use synchronous save to avoid deadlock
                return _currentSettings;
            }

            var json = File.ReadAllText(_settingsFilePath);
            _currentSettings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            
            Log.Information("Settings loaded from {Path}", _settingsFilePath);
            return _currentSettings;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load settings, using defaults");
            _currentSettings = new AppSettings();
            return _currentSettings;
        }
    }

    public async Task SaveAsync()
    {
        try
        {
            // Ensure config directory exists
            Directory.CreateDirectory(_configDirectory);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(_currentSettings, options);
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

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(_currentSettings, options);
            File.WriteAllText(_settingsFilePath, json);
            
            Log.Information("Settings saved to {Path}", _settingsFilePath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save settings");
            throw;
        }
    }
}
