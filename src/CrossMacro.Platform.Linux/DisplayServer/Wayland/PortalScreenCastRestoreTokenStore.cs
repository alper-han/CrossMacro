namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class PortalScreenCastRestoreTokenStore : IPortalScreenCastRestoreTokenStore, IPortalScreenCastRestoreStateService
{
    private const string StateFileName = "portal-screen-cast-state.json";
    private const int MaximumStateFileBytes = 128 * 1024;
    private readonly string _configDirectory;
    private readonly string _contextKey;
    private readonly string _stateFilePath;

    public PortalScreenCastRestoreTokenStore()
        : this(CrossMacro.Core.PathHelper.GetConfigDirectory(), LinuxEnvironmentVariables.CaptureCurrentSnapshot())
    {
    }

    internal PortalScreenCastRestoreTokenStore(string configDirectory, ILinuxEnvironmentVariables environmentVariables)
        : this(configDirectory, (environmentVariables ?? throw new ArgumentNullException(nameof(environmentVariables))).CaptureSnapshot())
    {
    }

    internal PortalScreenCastRestoreTokenStore(string configDirectory, LinuxEnvironmentSnapshot environment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configDirectory);
        _configDirectory = configDirectory;
        _contextKey = PortalScreenCastRestoreContext.Create(environment);
        _stateFilePath = Path.Combine(configDirectory, StateFileName);
    }

    public async Task<string?> LoadRestoreTokenAsync(CancellationToken cancellationToken)
    {
        var state = await LoadCurrentStateAsync(cancellationToken).ConfigureAwait(false);
        return state.RestoreToken;
    }

    public async Task<string?> LoadRestoreDataAsync(CancellationToken cancellationToken)
    {
        var state = await LoadCurrentStateAsync(cancellationToken).ConfigureAwait(false);
        return state.RestoreData;
    }

    public Task SaveRestoreTokenAsync(string restoreToken) => SaveCurrentStateAsync(restoreToken, restoreData: null);

    public Task SaveRestoreDataAsync(string restoreData) => SaveCurrentStateAsync(restoreToken: null, restoreData);

    public async Task ClearRestoreTokenAsync()
    {
        var state = await ReadStateAsync(CancellationToken.None).ConfigureAwait(false);
        if (!state.HasRestoreState && File.Exists(_stateFilePath))
        {
            return;
        }

        await WriteStateAsync(PortalScreenCastRestoreState.Empty, CancellationToken.None).ConfigureAwait(false);
    }

    public async Task<bool> HasRestoreStateAsync(CancellationToken cancellationToken)
    {
        using var lease = await PortalScreenCastRestoreTokenLease.AcquireAsync(_configDirectory, cancellationToken).ConfigureAwait(false);
        return (await LoadCurrentStateAsync(cancellationToken).ConfigureAwait(false)).HasRestoreState;
    }

    public async Task ClearRestoreStateAsync(CancellationToken cancellationToken)
    {
        using var lease = await PortalScreenCastRestoreTokenLease.AcquireAsync(_configDirectory, cancellationToken).ConfigureAwait(false);
        await ClearRestoreTokenAsync().ConfigureAwait(false);
    }

    private async Task SaveCurrentStateAsync(string? restoreToken, string? restoreData)
    {
        if (string.IsNullOrWhiteSpace(restoreToken) && string.IsNullOrWhiteSpace(restoreData))
        {
            return;
        }

        var current = await LoadCurrentStateAsync(CancellationToken.None).ConfigureAwait(false);
        var next = new PortalScreenCastRestoreState
        {
            RestoreToken = string.IsNullOrWhiteSpace(restoreToken) ? current.RestoreToken : restoreToken,
            RestoreData = string.IsNullOrWhiteSpace(restoreData) ? current.RestoreData : restoreData,
            Context = _contextKey,
        };

        if (next == current)
        {
            return;
        }

        await WriteStateAsync(next, CancellationToken.None).ConfigureAwait(false);
    }

    private async Task<PortalScreenCastRestoreState> LoadCurrentStateAsync(CancellationToken cancellationToken)
    {
        var state = await ReadStateAsync(cancellationToken).ConfigureAwait(false);
        if (!state.HasRestoreState || StringComparer.Ordinal.Equals(state.Context, _contextKey))
        {
            return state;
        }

        if (!string.IsNullOrWhiteSpace(state.Context))
        {
            return PortalScreenCastRestoreState.Empty;
        }

        var migratedState = state with { Context = _contextKey };
        await WriteStateAsync(migratedState, cancellationToken).ConfigureAwait(false);
        return migratedState;
    }

    private async Task<PortalScreenCastRestoreState> ReadStateAsync(CancellationToken cancellationToken)
    {
        return File.Exists(_stateFilePath)
            ? await ReadStateFileAsync(_stateFilePath, cancellationToken).ConfigureAwait(false)
            : PortalScreenCastRestoreState.Empty;
    }

    private static async Task<PortalScreenCastRestoreState> ReadStateFileAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var length = new FileInfo(path).Length;
            if (length is <= 0 or > MaximumStateFileBytes)
            {
                return PortalScreenCastRestoreState.Empty;
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
            return await JsonSerializer.DeserializeAsync(stream, PortalScreenCastRestoreStateJsonContext.Default.PortalScreenCastRestoreState, cancellationToken).ConfigureAwait(false)
                ?? PortalScreenCastRestoreState.Empty;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            Log.Warning(ex, "Could not read Portal ScreenCast restore state.");
            return PortalScreenCastRestoreState.Empty;
        }
    }

    private async Task WriteStateAsync(PortalScreenCastRestoreState state, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_stateFilePath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{StateFileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                if (OperatingSystem.IsLinux())
                {
                    File.SetUnixFileMode(temporaryPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }
                await JsonSerializer.SerializeAsync(stream, state, PortalScreenCastRestoreStateJsonContext.Default.PortalScreenCastRestoreState, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, _stateFilePath, overwrite: true);
        }
        finally
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Debug(ex, "Could not remove temporary Portal ScreenCast restore state.");
            }
        }
    }

}
