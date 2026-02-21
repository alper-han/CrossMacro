using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using Serilog;
using CrossMacro.Infrastructure.Serialization;
using CrossMacro.Infrastructure.Helpers;

namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Service for managing text expansion storage in a separate JSON file
/// Follows XDG Base Directory specification
/// </summary>
public class TextExpansionStorageService : ITextExpansionStorageService

{
    private const string ExpansionsFileName = ConfigFileNames.TextExpansions;
    private readonly string _configDirectory;
    private readonly string _filePath;
    private List<Core.Models.TextExpansion> _expansions = new();
    private readonly Lock _lock = new();

    public TextExpansionStorageService()
    {
        _configDirectory = PathHelper.GetConfigDirectory();
        _filePath = Path.Combine(_configDirectory, ExpansionsFileName);
        

        
        Log.Information("[TextExpansionStorageService] Storage path: {Path}", _filePath);
    }


    /// <summary>
    /// Loads all text expansions from the JSON file synchronously
    /// </summary>
    public List<Core.Models.TextExpansion> Load()
    {
        lock (_lock)
        {
            try
            {
                if (!File.Exists(_filePath))
                {
                    Log.Information("[TextExpansionStorageService] No existing file found, starting with empty list");
                    _expansions = [];
                    return new List<Core.Models.TextExpansion>(_expansions);
                }

                var json = File.ReadAllText(_filePath);
                _expansions = JsonSerializer.Deserialize(json, CrossMacroJsonContext.Default.ListTextExpansion) ?? [];
                
                Log.Information("[TextExpansionStorageService] Loaded {Count} text expansions", _expansions.Count);
                return new List<Core.Models.TextExpansion>(_expansions);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[TextExpansionStorageService] Failed to load text expansions");
                _expansions = [];
                return new List<Core.Models.TextExpansion>(_expansions);
            }
        }
    }

    /// <summary>
    /// Loads all text expansions from the JSON file asynchronously
    /// </summary>
    public async Task<List<Core.Models.TextExpansion>> LoadAsync()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                Log.Information("[TextExpansionStorageService] No existing file found, starting with empty list");
                lock (_lock) { _expansions = []; }
                return [];
            }

            var json = await File.ReadAllTextAsync(_filePath);
            var loaded = JsonSerializer.Deserialize(json, CrossMacroJsonContext.Default.ListTextExpansion) ?? [];
            
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
            lock (_lock) { _expansions = []; }
            return [];
        }
    }

    /// <summary>
    /// Saves all text expansions to the JSON file
    /// </summary>
    public async Task SaveAsync(IEnumerable<Core.Models.TextExpansion> expansions)
    {
        try
        {
            // Ensure config directory exists
            Directory.CreateDirectory(_configDirectory);
            
            var expansionList = expansions.ToList();
            
            var json = JsonSerializer.Serialize(expansionList, CrossMacroJsonContext.Default.ListTextExpansion);
            await File.WriteAllTextAsync(_filePath, json);
            
            lock (_lock)
            {
                _expansions = new List<Core.Models.TextExpansion>(expansionList);
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
    public List<Core.Models.TextExpansion> GetCurrent()
    {
        lock (_lock)
        {
            return new List<Core.Models.TextExpansion>(_expansions);
        }
    }

    /// <summary>
    /// Gets the file path where expansions are stored
    /// </summary>
    public string FilePath => _filePath;
}
