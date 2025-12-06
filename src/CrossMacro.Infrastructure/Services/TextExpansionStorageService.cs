using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using Serilog;

namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Service for managing text expansion storage in a separate JSON file
/// Follows XDG Base Directory specification
/// </summary>
public class TextExpansionStorageService
{
    private const string AppName = "crossmacro";
    private const string ExpansionsFileName = "text-expansions.json";
    private readonly string _configDirectory;
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions;
    private List<TextExpansion> _expansions = new();
    private readonly object _lock = new();

    public TextExpansionStorageService()
    {
        // Follow XDG Base Directory specification
        var xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var configHome = string.IsNullOrEmpty(xdgConfigHome)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config")
            : xdgConfigHome;

        _configDirectory = Path.Combine(configHome, AppName);
        _filePath = Path.Combine(_configDirectory, ExpansionsFileName);
        
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        
        Log.Information("[TextExpansionStorageService] Storage path: {Path}", _filePath);
    }


    /// <summary>
    /// Loads all text expansions from the JSON file synchronously
    /// </summary>
    public List<TextExpansion> Load()
    {
        lock (_lock)
        {
            try
            {
                if (!File.Exists(_filePath))
                {
                    Log.Information("[TextExpansionStorageService] No existing file found, starting with empty list");
                    _expansions = new List<TextExpansion>();
                    return new List<TextExpansion>(_expansions);
                }

                var json = File.ReadAllText(_filePath);
                _expansions = JsonSerializer.Deserialize<List<TextExpansion>>(json, _jsonOptions) ?? new List<TextExpansion>();
                
                Log.Information("[TextExpansionStorageService] Loaded {Count} text expansions", _expansions.Count);
                return new List<TextExpansion>(_expansions);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[TextExpansionStorageService] Failed to load text expansions");
                _expansions = new List<TextExpansion>();
                return new List<TextExpansion>(_expansions);
            }
        }
    }

    /// <summary>
    /// Loads all text expansions from the JSON file asynchronously
    /// </summary>
    public async Task<List<TextExpansion>> LoadAsync()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                Log.Information("[TextExpansionStorageService] No existing file found, starting with empty list");
                lock (_lock) { _expansions = new List<TextExpansion>(); }
                return new List<TextExpansion>();
            }

            var json = await File.ReadAllTextAsync(_filePath);
            var loaded = JsonSerializer.Deserialize<List<TextExpansion>>(json, _jsonOptions) ?? new List<TextExpansion>();
            
            lock (_lock)
            {
                _expansions = loaded;
            }
            
            Log.Information("[TextExpansionStorageService] Loaded {Count} text expansions", loaded.Count);
            return loaded;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[TextExpansionStorageService] Failed to load text expansions");
            lock (_lock) { _expansions = new List<TextExpansion>(); }
            return new List<TextExpansion>();
        }
    }

    /// <summary>
    /// Saves all text expansions to the JSON file
    /// </summary>
    public async Task SaveAsync(IEnumerable<TextExpansion> expansions)
    {
        try
        {
            // Ensure config directory exists
            Directory.CreateDirectory(_configDirectory);
            
            var expansionList = expansions.ToList();
            
            var json = JsonSerializer.Serialize(expansionList, _jsonOptions);
            await File.WriteAllTextAsync(_filePath, json);
            
            lock (_lock)
            {
                _expansions = new List<TextExpansion>(expansionList);
            }
            
            Log.Information("[TextExpansionStorageService] Saved {Count} text expansions", expansionList.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[TextExpansionStorageService] Failed to save text expansions");
            throw;
        }
    }


    /// <summary>
    /// Gets the current list of expansions (cached in memory)
    /// </summary>
    public List<TextExpansion> GetCurrent()
    {
        lock (_lock)
        {
            return new List<TextExpansion>(_expansions);
        }
    }

    /// <summary>
    /// Gets the file path where expansions are stored
    /// </summary>
    public string FilePath => _filePath;
}
