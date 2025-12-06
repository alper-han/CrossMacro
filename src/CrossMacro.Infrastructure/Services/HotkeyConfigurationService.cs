using System;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using Serilog;

namespace CrossMacro.Infrastructure.Services;

public class HotkeyConfigurationService : IHotkeyConfigurationService
{
    private readonly string _configPath;

    public HotkeyConfigurationService()
    {
        var xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        
        if (string.IsNullOrEmpty(xdgConfigHome))
        {
            xdgConfigHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
        }

        var configDir = Path.Combine(xdgConfigHome, "crossmacro");
        if (!Directory.Exists(configDir))
        {
            Directory.CreateDirectory(configDir);
        }

        _configPath = Path.Combine(configDir, "hotkeys.json");
    }

    public HotkeySettings Load()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                var settings = JsonSerializer.Deserialize<HotkeySettings>(json);
                if (settings != null)
                {
                    Log.Information("Loaded hotkey configuration from {Path}", _configPath);
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load hotkey configuration from {Path}", _configPath);
        }

        Log.Information("Using default hotkey configuration");
        return new HotkeySettings();
    }

    public async Task<HotkeySettings> LoadAsync()
    {
        try
        {
            if (!File.Exists(_configPath))
            {
                Log.Information("Using default hotkey configuration");
                return new HotkeySettings();
            }

            var json = await File.ReadAllTextAsync(_configPath);
            var settings = JsonSerializer.Deserialize<HotkeySettings>(json);
            if (settings != null)
            {
                Log.Information("Loaded hotkey configuration from {Path}", _configPath);
                return settings;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load hotkey configuration from {Path}", _configPath);
        }

        Log.Information("Using default hotkey configuration");
        return new HotkeySettings();
    }

    public void Save(HotkeySettings settings)
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(settings, options);
            File.WriteAllText(_configPath, json);
            Log.Information("Saved hotkey configuration to {Path}", _configPath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save hotkey configuration to {Path}", _configPath);
        }
    }
}
