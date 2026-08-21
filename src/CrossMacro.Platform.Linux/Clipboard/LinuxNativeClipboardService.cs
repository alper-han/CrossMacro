namespace CrossMacro.Platform.Linux.Clipboard;

/// <summary>
/// Linux clipboard service backed directly by the native display protocol.
/// It intentionally has no Avalonia or command-line tool dependency.
/// </summary>
public sealed class LinuxNativeClipboardService(LinuxEnvironmentSnapshot environment) :
    IClipboardService,
    IImageClipboardService,
    IImageClipboardReader,
    ILinuxClipboardService,
    IDisposable
{
    private readonly LinuxEnvironmentSnapshot _environment = environment;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private INativeLinuxClipboardBackend? _backend;
    private bool _initialized;
    private bool _disposed;

    public bool IsSupported => !_disposed && (!_initialized || _backend?.IsSupported is true);

    bool IImageClipboardReader.IsSupported => false;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_initialized)
        {
            return;
        }

        await _initializationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized)
            {
                return;
            }

            var waylandSession = IsWaylandSession(_environment);
            var candidates = waylandSession
                ? new List<INativeLinuxClipboardBackend>
                {
                    new WaylandNativeClipboardBackend(),
                }
                : new List<INativeLinuxClipboardBackend>
                {
                    new X11NativeClipboardBackend(),
                };

            if (waylandSession && !string.IsNullOrWhiteSpace(_environment.Display))
            {
                candidates.Add(new X11NativeClipboardBackend());
            }

            foreach (var candidate in candidates)
            {
                try
                {
                    await candidate.InitializeAsync(cancellationToken).ConfigureAwait(false);
                    if (candidate.IsSupported)
                    {
                        _backend = candidate;
                        Log.Information("[LinuxNativeClipboard] Selected {BackendName} backend", candidate.BackendName);
                        break;
                    }
                }
                catch (OperationCanceledException)
                {
                    candidate.Dispose();
                    throw;
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    Log.Debug(ex, "[LinuxNativeClipboard] {BackendName} backend is unavailable", candidate.BackendName);
                }

                candidate.Dispose();
            }

            _initialized = true;
            if (_backend is null)
            {
                Log.Warning("[LinuxNativeClipboard] No native clipboard backend is available for the current display session");
            }
        }
        finally
        {
            _ = _initializationLock.Release();
        }
    }

    public async Task SetTextAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        var backend = await GetBackendAsync(cancellationToken).ConfigureAwait(false);
        await backend.SetTextAsync(text, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> GetTextAsync(CancellationToken cancellationToken = default)
    {
        var backend = await GetBackendAsync(cancellationToken).ConfigureAwait(false);
        return await backend.GetTextAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SetPngAsync(ReadOnlyMemory<byte> pngBytes, CancellationToken cancellationToken = default)
    {
        try
        {
            var backend = await GetBackendAsync(cancellationToken).ConfigureAwait(false);
            await backend.SetPngAsync(pngBytes, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex) when (_initialized && _backend is null)
        {
            throw new ImageClipboardUnavailableException(ex.Message, ex);
        }
    }

    public Task<byte[]?> GetPngAsync(int maximumBytes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (maximumBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumBytes), "Maximum PNG bytes must be positive.");
        }

        throw new ImageClipboardUnavailableException("PNG image clipboard reading is not supported by the native Linux clipboard backend.");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _backend?.Dispose();
        _backend = null;
        _initializationLock.Dispose();
    }

    private async Task<INativeLinuxClipboardBackend> GetBackendAsync(CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        return _backend ?? throw new InvalidOperationException(
            "No native Linux clipboard backend is available for the current display session.");
    }

    private static bool IsWaylandSession(LinuxEnvironmentSnapshot environment)
    {
        if (string.Equals(environment.SessionType, "wayland", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(environment.SessionType, "x11", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(environment.WaylandDisplay);
    }
}
