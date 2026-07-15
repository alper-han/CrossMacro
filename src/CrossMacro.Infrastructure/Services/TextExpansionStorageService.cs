
namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Service for managing text expansion storage in a separate JSON file
/// Follows XDG Base Directory specification
/// </summary>
public class TextExpansionStorageService : ITextExpansionStorageService

{
    private const string ExpansionsFileName = ConfigFileNames.TextExpansions;
    private string _filePath;
    private List<Core.Models.TextExpansionEntry> _expansions = new();
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
    public IList<Core.Models.TextExpansionEntry> Load()
    {
        lock (_lock)
        {
            try
            {
                if (!File.Exists(_filePath))
                {
                    Log.Information("[TextExpansionStorageService] No existing file found, starting with empty list");
                    _expansions = [];
                    return _expansions;
                }

                _expansions = FileBackedJsonStorage.Read(_filePath, CrossMacroJsonContext.Default.ListTextExpansionEntry) ?? [];

                Log.Information("[TextExpansionStorageService] Loaded {Count} text expansions", _expansions.Count);
                return _expansions;
            }
            catch (Exception ex)
            {
                Log.LogError(ex, "[TextExpansionStorageService] Failed to load text expansions");
                _expansions = [];
                return _expansions;
            }
        }
    }

    /// <summary>
    /// Loads all text expansions from the JSON file asynchronously
    /// </summary>
    public async Task<IList<Core.Models.TextExpansionEntry>> LoadAsync()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                Log.Information("[TextExpansionStorageService] No existing file found, starting with empty list");
                lock (_lock) { _expansions = []; }
                return new List<Core.Models.TextExpansionEntry>();
            }

            var loaded = await FileBackedJsonStorage.ReadAsync(_filePath, CrossMacroJsonContext.Default.ListTextExpansionEntry)
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
            Log.LogError(ex, "[TextExpansionStorageService] Failed to load text expansions");
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
    public async Task SaveAsync(IEnumerable<Core.Models.TextExpansionEntry> expansions)
    {
        try
        {
            var expansionList = expansions.ToList();

            await FileBackedJsonStorage.WriteAsync(_filePath, expansionList, CrossMacroJsonContext.Default.ListTextExpansionEntry)
                .ConfigureAwait(false);

            lock (_lock)
            {
                _expansions = new List<Core.Models.TextExpansionEntry>(expansionList);
            }

            Log.Information("[TextExpansionStorageService] Saved {Count} text expansions", expansionList.Count);
        }
        catch (Exception ex)
        {
            Log.LogError(ex, "[TextExpansionStorageService] Failed to save text expansions");
            throw;
        }
    }


    /// <summary>
    /// Gets the current list of expansions (cached in memory)
    /// </summary>
    public IList<Core.Models.TextExpansionEntry> GetCurrent()
    {
        lock (_lock)
        {
            return new List<Core.Models.TextExpansionEntry>(_expansions);
        }
    }

    /// <summary>
    /// Gets the file path where expansions are stored
    /// </summary>
    public string FilePath => _filePath;
}
