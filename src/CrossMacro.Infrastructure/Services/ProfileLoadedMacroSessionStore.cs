using CrossMacro.Infrastructure.Persistence.Macros;

namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Profile-scoped loaded-macro session persistence.
/// </summary>
internal sealed class ProfileLoadedMacroSessionStore(IMacroFileManager macroFileManager) : IProfileLoadedMacroSessionStore, IDisposable
{
    private readonly IMacroFileManager _macroFileManager = macroFileManager ?? throw new ArgumentNullException(nameof(macroFileManager));
    private readonly SemaphoreSlim _fileGate = new(1, 1);

    public async Task<LoadedMacroSessionSnapshot> LoadAsync(
        string profileConfigDirectory,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(profileConfigDirectory);
        await _fileGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(filePath))
            {
                return LoadedMacroSessionSnapshot.Empty;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var persisted = await FileBackedJsonStorage.ReadAsync(
                    filePath,
                    CrossMacroJsonContext.Default.PersistedLoadedMacroSession)
                .ConfigureAwait(false);

            if (persisted is null)
            {
                return LoadedMacroSessionSnapshot.Empty;
            }

            if (persisted.SchemaVersion > PersistedLoadedMacroSession.CurrentSchemaVersion)
            {
                throw new InvalidOperationException(
                    $"Unsupported loaded macro session schema version {persisted.SchemaVersion.ToString(CultureInfo.InvariantCulture)}.");
            }

            var seenSessionIds = new HashSet<Guid>();
            var availableSessionIds = new HashSet<Guid>();
            var items = new List<LoadedMacroSessionItemSnapshot>(persisted.Items.Count);
            foreach (var item in persisted.Items)
            {
                if (item.SessionId == Guid.Empty || !seenSessionIds.Add(item.SessionId))
                {
                    throw new InvalidDataException("The loaded macro session contains an empty or duplicate session id.");
                }

                if (string.IsNullOrWhiteSpace(item.SourcePath) || !Path.IsPathFullyQualified(item.SourcePath))
                {
                    Log.Warning("Skipping unavailable loaded macro {SourcePath} for profile directory {ProfileDirectory}", item.SourcePath, profileConfigDirectory);
                    continue;
                }

                if (!File.Exists(item.SourcePath))
                {
                    Log.Warning("Skipping unavailable loaded macro {SourcePath} for profile directory {ProfileDirectory}", item.SourcePath, profileConfigDirectory);
                    continue;
                }

                MacroSequence? macro;
                try
                {
                    macro = await _macroFileManager.LoadAsync(item.SourcePath).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    Log.Warning(ex, "Skipping unreadable loaded macro {SourcePath} for profile directory {ProfileDirectory}", item.SourcePath, profileConfigDirectory);
                    continue;
                }

                if (macro is null)
                {
                    Log.Warning("Skipping unreadable loaded macro {SourcePath} for profile directory {ProfileDirectory}", item.SourcePath, profileConfigDirectory);
                    continue;
                }

                items.Add(new LoadedMacroSessionItemSnapshot(
                    item.SessionId,
                    macro,
                    item.SourcePath,
                    Math.Max(1, item.SequenceRepeatCount)));
                _ = availableSessionIds.Add(item.SessionId);
            }

            var selectedSessionId = persisted.SelectedSessionId is { } selectedId && availableSessionIds.Contains(selectedId)
                ? (Guid?)selectedId
                : null;
            return new LoadedMacroSessionSnapshot(items, selectedSessionId, persisted.PlaybackMode);
        }
        finally
        {
            _ = _fileGate.Release();
        }
    }

    public async Task SaveAsync(
        string profileConfigDirectory,
        LoadedMacroSessionSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var filePath = GetFilePath(profileConfigDirectory);
        var sessionIds = new HashSet<Guid>();
        var persistedSessionIds = new HashSet<Guid>();
        var items = new List<PersistedLoadedMacroSessionItem>(snapshot.Items.Count);
        foreach (var item in snapshot.Items)
        {
            if (item.SessionId == Guid.Empty || !sessionIds.Add(item.SessionId))
            {
                throw new InvalidDataException("The loaded macro session contains an empty or duplicate session id.");
            }

            if (string.IsNullOrWhiteSpace(item.SourcePath)
                || !Path.IsPathFullyQualified(item.SourcePath)
                || !File.Exists(item.SourcePath))
            {
                Log.Warning("Skipping unavailable loaded macro {SourcePath} for profile directory {ProfileDirectory}", item.SourcePath, profileConfigDirectory);
                continue;
            }

            items.Add(new PersistedLoadedMacroSessionItem
            {
                SessionId = item.SessionId,
                SourcePath = item.SourcePath,
                SequenceRepeatCount = Math.Max(1, item.SequenceRepeatCount),
            });
            _ = persistedSessionIds.Add(item.SessionId);
        }

        var persisted = new PersistedLoadedMacroSession
        {
            Items = items,
            SelectedSessionId = snapshot.SelectedSessionId is { } selectedSessionId && persistedSessionIds.Contains(selectedSessionId)
                ? selectedSessionId
                : null,
            PlaybackMode = snapshot.PlaybackMode,
        };

        await _fileGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await FileBackedJsonStorage.WriteAsync(
                    filePath,
                    persisted,
                    CrossMacroJsonContext.Default.PersistedLoadedMacroSession,
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
        return Path.Combine(profileConfigDirectory, ConfigFileNames.LoadedMacros);
    }
}
