namespace CrossMacro.Infrastructure.Services.TextExpansion;

/// <summary>
/// Profile-scoped text expansion persistence that never mutates the active
/// <see cref="TextExpansionStorageService"/> path.
/// </summary>
internal sealed class ProfileTextExpansionStore : IProfileTextExpansionStore, IDisposable
{
    private readonly SemaphoreSlim _fileGate = new(1, 1);

    public async Task<IList<TextExpansionEntry>> LoadAsync(
        string profileConfigDirectory,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(profileConfigDirectory);
        await _fileGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(filePath))
            {
                return [];
            }

            cancellationToken.ThrowIfCancellationRequested();
            return await FileBackedJsonStorage.ReadAsync(
                    filePath,
                    CrossMacroJsonContext.Default.ListTextExpansionEntry)
                .ConfigureAwait(false)
                ?? [];
        }
        finally
        {
            _ = _fileGate.Release();
        }
    }

    public async Task SaveAsync(
        string profileConfigDirectory,
        IEnumerable<TextExpansionEntry> expansions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expansions);
        var filePath = GetFilePath(profileConfigDirectory);
        var snapshot = expansions.ToList();

        await _fileGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await FileBackedJsonStorage.WriteAsync(
                    filePath,
                    snapshot,
                    CrossMacroJsonContext.Default.ListTextExpansionEntry,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _ = _fileGate.Release();
        }
    }

    public void Dispose() => _fileGate.Dispose();

    private static string GetFilePath(string profileConfigDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileConfigDirectory);
        return Path.Combine(profileConfigDirectory, ConfigFileNames.TextExpansions);
    }
}
