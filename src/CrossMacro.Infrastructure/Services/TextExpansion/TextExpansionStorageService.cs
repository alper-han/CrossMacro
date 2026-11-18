namespace CrossMacro.Infrastructure.Services.TextExpansion;

/// <summary>
/// The text expansions of the active profile, cached in memory. File access is delegated to
/// <see cref="IProfileTextExpansionStore"/>. When the file cannot be loaded the cache stays unloaded and
/// saving is refused, so the unreadable file is never overwritten with an empty list.
/// </summary>
public sealed class TextExpansionStorageService : ITextExpansionStorageService, IDisposable
{
    private readonly IProfileTextExpansionStore _files;
    private readonly Lock _lock = new();
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private List<TextExpansionEntry> _expansions = [];
    private string _profileConfigDirectory;
    private bool _loadFailed;

    public TextExpansionStorageService(string configDirectory, IProfileTextExpansionStore files)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configDirectory);
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _profileConfigDirectory = configDirectory;

        Log.Information("[TextExpansionStorageService] Storage path: {Path}", FilePath);
    }

    /// <summary>
    /// Gets the file path where expansions are stored
    /// </summary>
    public string FilePath
    {
        get
        {
            lock (_lock)
            {
                return Path.Combine(_profileConfigDirectory, ConfigFileNames.TextExpansions);
            }
        }
    }

    public bool IsLoaded
    {
        get
        {
            lock (_lock)
            {
                return field;
            }
        }

        private set;
    }

    /// <summary>
    /// Gets a copy of the cached expansions
    /// </summary>
    public IList<TextExpansionEntry> GetCurrent()
    {
        lock (_lock)
        {
            return new List<TextExpansionEntry>(_expansions);
        }
    }

    /// <summary>
    /// Loads all text expansions from the JSON file
    /// </summary>
    public async Task<IList<TextExpansionEntry>> LoadAsync()
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

    public async Task ReloadAsync(string profileConfigDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileConfigDirectory);
        await _operationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            lock (_lock)
            {
                _profileConfigDirectory = profileConfigDirectory;
                _expansions = [];
                IsLoaded = false;
                _loadFailed = false;
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
    public async Task SaveAsync(IEnumerable<TextExpansionEntry> expansions)
    {
        ArgumentNullException.ThrowIfNull(expansions);
        var expansionList = expansions.ToList();
        await _operationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            string directory;
            lock (_lock)
            {
                if (_loadFailed)
                {
                    throw new InvalidOperationException("The text expansions could not be loaded; saving would overwrite the unreadable file.");
                }

                directory = _profileConfigDirectory;
            }

            await _files.SaveAsync(directory, expansionList, CancellationToken.None).ConfigureAwait(false);

            lock (_lock)
            {
                _expansions = expansionList;
                IsLoaded = true;
            }

            Log.Information("[TextExpansionStorageService] Saved {Count} text expansions", expansionList.Count);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "[TextExpansionStorageService] Failed to save text expansions");
            throw;
        }
        finally
        {
            _ = _operationGate.Release();
        }
    }

    public void Dispose() => _operationGate.Dispose();

    private async Task<IList<TextExpansionEntry>> LoadCoreAsync()
    {
        string directory;
        lock (_lock)
        {
            directory = _profileConfigDirectory;
        }

        try
        {
            var loaded = await _files.LoadAsync(directory, CancellationToken.None).ConfigureAwait(false);
            lock (_lock)
            {
                _expansions = [.. loaded];
                IsLoaded = true;
                _loadFailed = false;
            }

            Log.Information("[TextExpansionStorageService] Loaded {Count} text expansions", loaded.Count);
            return new List<TextExpansionEntry>(loaded);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "[TextExpansionStorageService] Failed to load text expansions; saving is disabled until a load succeeds");
            lock (_lock)
            {
                _expansions = [];
                IsLoaded = false;
                _loadFailed = true;
            }

            return [];
        }
    }
}
