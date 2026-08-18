namespace CrossMacro.UI.Services;

/// <summary>
/// Keeps the loaded macro session isolated to the active profile and restores it on profile activation.
/// </summary>
internal sealed class ProfileLoadedMacroSessionPersistenceService : IProfileRuntimeParticipant, IAsyncDisposable, IDisposable
{
    private static readonly TimeSpan SaveDebounce = TimeSpan.FromMilliseconds(500);
    private const string ProfileMacrosDirectoryName = "macros";
    private readonly ILoadedMacroSession _loadedMacroSession;
    private readonly IProfileLoadedMacroSessionStore _store;
    private readonly IMacroFileManager _macroFileManager;
    private readonly Lock _sync = new();
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private string? _profileConfigDirectory;
    private bool _canPersistCurrentProfile;
    private int _saveVersion;
    private int _profileVersion;
    private int _stateVersion;
    private int _persistedStateVersion;
    private int _suppressSave;
    private int _disposed;

    public ProfileLoadedMacroSessionPersistenceService(
        ILoadedMacroSession loadedMacroSession,
        IProfileLoadedMacroSessionStore store,
        IMacroFileManager macroFileManager)
    {
        _loadedMacroSession = loadedMacroSession ?? throw new ArgumentNullException(nameof(loadedMacroSession));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _macroFileManager = macroFileManager ?? throw new ArgumentNullException(nameof(macroFileManager));
        _loadedMacroSession.SessionStateChanged += OnSessionStateChanged;
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        _ = Interlocked.Increment(ref _saveVersion);
        var profileConfigDirectory = GetWritableProfileConfigDirectory();
        if (profileConfigDirectory is not null)
        {
            await SaveCurrentSnapshotAsync(profileConfigDirectory, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task ReloadAsync(string profileConfigDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileConfigDirectory);
        _ = Interlocked.Increment(ref _saveVersion);

        LoadedMacroSessionSnapshot snapshot;
        var canPersistLoadedProfile = true;
        try
        {
            snapshot = await _store.LoadAsync(profileConfigDirectory, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException)
        {
            Log.Warning(ex, "Failed to load the macro session for profile directory {ProfileDirectory}; starting with an empty session", profileConfigDirectory);
            snapshot = LoadedMacroSessionSnapshot.Empty;
            canPersistLoadedProfile = false;
        }

        // A profile switch can yield while its target state is loading. Persist every
        // edit made to the old profile before replacing the UI-bound collection.
        while (true)
        {
            var pendingSnapshot = await CapturePendingSnapshotAsync().ConfigureAwait(false);
            if (pendingSnapshot is not null)
            {
                await SaveSnapshotAsync(pendingSnapshot, cancellationToken).ConfigureAwait(false);
                continue;
            }

            var restored = await RunOnUiThreadAsync(() =>
            {
                if (HasPendingCurrentProfileChanges())
                {
                    return false;
                }

                RestoreSnapshot(profileConfigDirectory, snapshot, canPersistLoadedProfile);
                return true;
            }).ConfigureAwait(false);
            if (restored)
            {
                return;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCoreAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        DisposeAsyncCoreAsync().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    private async Task DisposeAsyncCoreAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) is not 0)
        {
            return;
        }

        _loadedMacroSession.SessionStateChanged -= OnSessionStateChanged;
        try
        {
            await FlushAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "Failed to persist the loaded macro session during disposal");
        }
        finally
        {
            _saveGate.Dispose();
        }
    }

    private void OnSessionStateChanged(object? sender, EventArgs e)
    {
        if (Volatile.Read(ref _suppressSave) is not 0 || Volatile.Read(ref _disposed) is not 0)
        {
            return;
        }

        _ = Interlocked.Increment(ref _stateVersion);
        if (GetWritableProfileConfigDirectory() is null)
        {
            return;
        }

        var saveVersion = Interlocked.Increment(ref _saveVersion);
        var profileVersion = GetCurrentProfileVersion();
        if (profileVersion is not null)
        {
            _ = SaveAfterIdleAsync(saveVersion, profileVersion.Value);
        }
    }

    private async Task SaveAfterIdleAsync(int saveVersion, int profileVersion)
    {
        try
        {
            await Task.Delay(SaveDebounce, TimeProvider.System, CancellationToken.None).ConfigureAwait(false);
            if (saveVersion != Volatile.Read(ref _saveVersion) || Volatile.Read(ref _disposed) is not 0)
            {
                return;
            }

            var profileConfigDirectory = GetWritableProfileConfigDirectory();
            if (profileConfigDirectory is not null)
            {
                await SaveCurrentSnapshotAsync(profileConfigDirectory, CancellationToken.None, saveVersion, profileVersion).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "Failed to persist the loaded macro session after an idle delay");
        }
    }

    private async Task SaveCurrentSnapshotAsync(
        string profileConfigDirectory,
        CancellationToken cancellationToken,
        int? expectedSaveVersion = null,
        int? expectedProfileVersion = null)
    {
        await _saveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (expectedSaveVersion is not null && expectedSaveVersion.Value != Volatile.Read(ref _saveVersion))
            {
                return;
            }

            var snapshot = await CaptureSnapshotAsync().ConfigureAwait(false);
            if (snapshot is null
                || !string.Equals(profileConfigDirectory, snapshot.ProfileConfigDirectory, StringComparison.Ordinal)
                || (expectedSaveVersion is not null && expectedSaveVersion.Value != Volatile.Read(ref _saveVersion))
                || (expectedProfileVersion is not null && expectedProfileVersion.Value != snapshot.ProfileVersion))
            {
                return;
            }

            var preparedSnapshot = await EnsureMacroFilesAsync(snapshot).ConfigureAwait(false);
            if (expectedSaveVersion is not null && expectedSaveVersion.Value != Volatile.Read(ref _saveVersion))
            {
                return;
            }

            await _store.SaveAsync(profileConfigDirectory, preparedSnapshot.Snapshot, cancellationToken).ConfigureAwait(false);
            MarkSnapshotPersisted(preparedSnapshot);
        }
        finally
        {
            _ = _saveGate.Release();
        }
    }

    private async Task<CapturedSessionSnapshot?> CapturePendingSnapshotAsync()
    {
        return await RunOnUiThreadAsync(() =>
        {
            var profileConfigDirectory = GetWritableProfileConfigDirectory();
            var stateVersion = Volatile.Read(ref _stateVersion);
            if (profileConfigDirectory is null || stateVersion == Volatile.Read(ref _persistedStateVersion))
            {
                return null;
            }

            return new CapturedSessionSnapshot(
                profileConfigDirectory,
                _loadedMacroSession.CreateSnapshot(),
                Volatile.Read(ref _profileVersion),
                stateVersion);
        }).ConfigureAwait(false);
    }

    private async Task SaveSnapshotAsync(CapturedSessionSnapshot snapshot, CancellationToken cancellationToken)
    {
        await _saveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var preparedSnapshot = await EnsureMacroFilesAsync(snapshot).ConfigureAwait(false);
            await _store.SaveAsync(preparedSnapshot.ProfileConfigDirectory, preparedSnapshot.Snapshot, cancellationToken).ConfigureAwait(false);
            MarkSnapshotPersisted(preparedSnapshot);
        }
        finally
        {
            _ = _saveGate.Release();
        }
    }

    private Task<CapturedSessionSnapshot?> CaptureSnapshotAsync() => RunOnUiThreadAsync(() =>
    {
        var profileConfigDirectory = GetWritableProfileConfigDirectory();
        return profileConfigDirectory is null
            ? null
            : new CapturedSessionSnapshot(
                profileConfigDirectory,
                _loadedMacroSession.CreateSnapshot(),
                Volatile.Read(ref _profileVersion),
                Volatile.Read(ref _stateVersion));
    });

    private bool HasPendingCurrentProfileChanges()
    {
        lock (_sync)
        {
            return _canPersistCurrentProfile
                && Volatile.Read(ref _stateVersion) != Volatile.Read(ref _persistedStateVersion);
        }
    }

    private string? GetWritableProfileConfigDirectory()
    {
        lock (_sync)
        {
            return _canPersistCurrentProfile ? _profileConfigDirectory : null;
        }
    }

    private int? GetCurrentProfileVersion()
    {
        lock (_sync)
        {
            return _canPersistCurrentProfile ? Volatile.Read(ref _profileVersion) : null;
        }
    }

    private void MarkSnapshotPersisted(CapturedSessionSnapshot snapshot)
    {
        lock (_sync)
        {
            if (_canPersistCurrentProfile
                && string.Equals(_profileConfigDirectory, snapshot.ProfileConfigDirectory, StringComparison.Ordinal)
                && Volatile.Read(ref _profileVersion) == snapshot.ProfileVersion
                && Volatile.Read(ref _stateVersion) == snapshot.StateVersion)
            {
                Volatile.Write(ref _persistedStateVersion, snapshot.StateVersion);
            }
        }
    }

    private void RestoreSnapshot(
        string profileConfigDirectory,
        LoadedMacroSessionSnapshot snapshot,
        bool canPersistLoadedProfile)
    {
        _ = Interlocked.Increment(ref _suppressSave);
        try
        {
            _loadedMacroSession.RestoreSnapshot(snapshot);
            lock (_sync)
            {
                _profileConfigDirectory = profileConfigDirectory;
                _canPersistCurrentProfile = canPersistLoadedProfile;
                _ = Interlocked.Increment(ref _profileVersion);
                Volatile.Write(ref _persistedStateVersion, Volatile.Read(ref _stateVersion));
            }
        }
        finally
        {
            _ = Interlocked.Decrement(ref _suppressSave);
        }
    }

    private sealed record CapturedSessionSnapshot(
        string ProfileConfigDirectory,
        LoadedMacroSessionSnapshot Snapshot,
        int ProfileVersion,
        int StateVersion);

    private async Task<CapturedSessionSnapshot> EnsureMacroFilesAsync(CapturedSessionSnapshot snapshot)
    {
        var profileMacrosDirectory = Path.Combine(snapshot.ProfileConfigDirectory, ProfileMacrosDirectoryName);
        var items = new List<LoadedMacroSessionItemSnapshot>(snapshot.Snapshot.Items.Count);
        var generatedPaths = new List<(Guid SessionId, MacroSequence Macro, string SourcePath)>();

        foreach (var item in snapshot.Snapshot.Items)
        {
            var sourcePath = item.SourcePath;
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                sourcePath = Path.Combine(profileMacrosDirectory, $"{item.SessionId:N}.macro");
                await _macroFileManager.SaveAsync(item.Macro, sourcePath).ConfigureAwait(false);
                generatedPaths.Add((item.SessionId, item.Macro, sourcePath));
            }
            else if (IsProfileMacroPath(sourcePath, profileMacrosDirectory) && File.Exists(sourcePath))
            {
                await _macroFileManager.SaveAsync(item.Macro, sourcePath).ConfigureAwait(false);
            }

            items.Add(item with { SourcePath = sourcePath });
        }

        if (generatedPaths.Count > 0)
        {
            _ = Interlocked.Increment(ref _suppressSave);
            try
            {
                _ = await RunOnUiThreadAsync(() =>
                {
                    foreach (var generatedPath in generatedPaths)
                    {
                        _ = _loadedMacroSession.UpdateMacro(
                            generatedPath.SessionId,
                            generatedPath.Macro,
                            generatedPath.SourcePath);
                    }

                    return true;
                }).ConfigureAwait(false);
            }
            finally
            {
                _ = Interlocked.Decrement(ref _suppressSave);
            }
        }

        return snapshot with
        {
            Snapshot = snapshot.Snapshot with { Items = items },
        };
    }

    private static bool IsProfileMacroPath(string sourcePath, string profileMacrosDirectory)
    {
        var macrosRoot = Path.GetFullPath(profileMacrosDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var fullSourcePath = Path.GetFullPath(sourcePath);
        return fullSourcePath.StartsWith(macrosRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<T> RunOnUiThreadAsync<T>(Func<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (Avalonia.Application.Current is null || Dispatcher.UIThread.CheckAccess())
        {
            return action();
        }

        return await Dispatcher.UIThread.InvokeAsync(action);
    }
}
