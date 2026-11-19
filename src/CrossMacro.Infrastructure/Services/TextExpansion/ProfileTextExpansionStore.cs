namespace CrossMacro.Infrastructure.Services.TextExpansion;

/// <summary>
/// Reads and writes the text expansion file of any profile directory. A file that cannot be parsed is moved
/// aside and read as empty, so a later save can never overwrite it; failures to read the file are not swallowed.
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

            try
            {
                return await FileBackedJsonStorage.ReadAsync(
                        filePath,
                        CrossMacroJsonContext.Default.ListTextExpansionEntry,
                        cancellationToken)
                    .ConfigureAwait(false)
                    ?? [];
            }
            catch (JsonException ex)
            {
                var backupPath = FileBackedJsonStorage.MoveAsideCorruptFile(filePath);
                Log.LogError(ex, "Text expansion file {Path} is malformed; it was kept as {BackupPath} and an empty list is used", filePath, backupPath);
                return [];
            }
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
