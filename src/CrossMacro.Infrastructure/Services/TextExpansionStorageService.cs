using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Logging;
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
    private string _filePath;
    private List<Core.Models.TextExpansion> _expansions = new();
    private readonly Lock _lock = new();

    public TextExpansionStorageService(string? configDirectory = null)
    {
        configDirectory = string.IsNullOrWhiteSpace(configDirectory)
            ? PathHelper.GetConfigDirectory()
            : configDirectory;
        _filePath = Path.Combine(configDirectory, ExpansionsFileName);
        

        
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

                _expansions = FileBackedJsonStorage.Read(_filePath, CrossMacroJsonContext.Default.ListTextExpansion) ?? [];
                
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

            var loaded = await FileBackedJsonStorage.ReadAsync(_filePath, CrossMacroJsonContext.Default.ListTextExpansion)
                .ConfigureAwait(false)
                ?? [];
            
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

    public async Task ReloadAsync(string profileConfigDirectory)
    {
        lock (_lock)
        {
            _filePath = Path.Combine(profileConfigDirectory, ConfigFileNames.TextExpansions);
            _expansions = [];
        }

        await LoadAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Saves all text expansions to the JSON file
    /// </summary>
    public async Task SaveAsync(IEnumerable<Core.Models.TextExpansion> expansions)
    {
        try
        {
            var expansionList = expansions.ToList();

            await FileBackedJsonStorage.WriteAsync(_filePath, expansionList, CrossMacroJsonContext.Default.ListTextExpansion)
                .ConfigureAwait(false);
            
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
