
namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Service for managing text expansion storage in a separate JSON file
/// Follows XDG Base Directory specification
/// </summary>
public class TextExpansionStorageService : ITextExpansionStorageService, IDisposable

{
    private const string ExpansionsFileName = ConfigFileNames.TextExpansions;
    private List<Core.Models.TextExpansionEntry> _expansions = new();
    private readonly Lock _lock = new();
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private int _loadedState;

    public TextExpansionStorageService(string? configDirectory = null)
    {
        configDirectory = string.IsNullOrWhiteSpace(configDirectory)
            ? PathHelper.GetConfigDirectory()
            : configDirectory;
        FilePath = Path.Combine(configDirectory, ExpansionsFileName);


        Log.Information("[TextExpansionStorageService] Storage path: {Path}", FilePath);
    }


    /// <summary>
    /// Loads all text expansions from the JSON file synchronously
    /// </summary>
    public IList<Core.Models.TextExpansionEntry> Load()
    {
        _operationGate.Wait();
        try
        {
            return LoadCore();
        }
        finally
        {
            _ = _operationGate.Release();
        }
    }

    private IList<Core.Models.TextExpansionEntry> LoadCore()
    {
        lock (_lock)
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    Log.Information("[TextExpansionStorageService] No existing file found, starting with empty list");
                    _expansions = [];
                    Volatile.Write(ref _loadedState, 1);
                    return _expansions;
                }

                _expansions = FileBackedJsonStorage.Read(FilePath, CrossMacroJsonContext.Default.ListTextExpansionEntry) ?? [];
                Volatile.Write(ref _loadedState, 1);

                Log.Information("[TextExpansionStorageService] Loaded {Count} text expansions", _expansions.Count);
                return new List<Core.Models.TextExpansionEntry>(_expansions);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.LogError(ex, "[TextExpansionStorageService] Failed to load text expansions");
                _expansions = [];
                Volatile.Write(ref _loadedState, 1);
                return new List<Core.Models.TextExpansionEntry>(_expansions);
            }
        }
    }

    /// <summary>
    /// Loads all text expansions from the JSON file asynchronously
    /// </summary>
    public async Task<IList<Core.Models.TextExpansionEntry>> LoadAsync()
    {
        await _operationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            return await LoadCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            _ = _operationGate.Release();
        }
    }

    private async Task<IList<Core.Models.TextExpansionEntry>> LoadCoreAsync()
    {
        string filePath;
        lock (_lock)
        {
            filePath = FilePath;
        }
        try
        {
            if (!File.Exists(filePath))
            {
                Log.Information("[TextExpansionStorageService] No existing file found, starting with empty list");
                lock (_lock)
                {
                    if (string.Equals(FilePath, filePath, StringComparison.Ordinal))
                    {
                        _expansions = [];
                        Volatile.Write(ref _loadedState, 1);
                    }
                }

                return new List<Core.Models.TextExpansionEntry>();
            }

            var loaded = await FileBackedJsonStorage.ReadAsync(filePath, CrossMacroJsonContext.Default.ListTextExpansionEntry)
                .ConfigureAwait(false)
                ?? [];

            lock (_lock)
            {
                if (string.Equals(FilePath, filePath, StringComparison.Ordinal))
                {
                    _expansions = loaded;
                    Volatile.Write(ref _loadedState, 1);
                }
            }

            Log.Information("[TextExpansionStorageService] Loaded {Count} text expansions", loaded.Count);
            return new List<Core.Models.TextExpansionEntry>(loaded);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "[TextExpansionStorageService] Failed to load text expansions");
            lock (_lock)
            {
                if (string.Equals(FilePath, filePath, StringComparison.Ordinal))
                {
                    _expansions = [];
                    Volatile.Write(ref _loadedState, 1);
                }
            }

            return [];
        }
    }

    public async Task ReloadAsync(string profileConfigDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileConfigDirectory);
        await _operationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            lock (_lock)
            {
                FilePath = Path.Combine(profileConfigDirectory, ConfigFileNames.TextExpansions);
                _expansions = [];
                Volatile.Write(ref _loadedState, 0);
            }

            _ = await LoadCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            _ = _operationGate.Release();
        }
    }

    /// <summary>
    /// Saves all text expansions to the JSON file
    /// </summary>
    public async Task SaveAsync(IEnumerable<Core.Models.TextExpansionEntry> expansions)
    {
        ArgumentNullException.ThrowIfNull(expansions);
        try
        {
            var expansionList = expansions.ToList();
            await _operationGate.WaitAsync().ConfigureAwait(false);
            try
            {
                string filePath;
                lock (_lock)
                {
                    filePath = FilePath;
                }

                await FileBackedJsonStorage.WriteAsync(filePath, expansionList, CrossMacroJsonContext.Default.ListTextExpansionEntry)
                    .ConfigureAwait(false);

                lock (_lock)
                {
                    _expansions = new List<Core.Models.TextExpansionEntry>(expansionList);
                    Volatile.Write(ref _loadedState, 1);
                }

                Log.Information("[TextExpansionStorageService] Saved {Count} text expansions", expansionList.Count);
            }
            finally
            {
                _ = _operationGate.Release();
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
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

    public bool IsLoaded => Volatile.Read(ref _loadedState) is 1;

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _operationGate.Dispose();
        }
    }

    /// <summary>
    /// Gets the file path where expansions are stored
    /// </summary>
    public string FilePath { get; private set; }
}
