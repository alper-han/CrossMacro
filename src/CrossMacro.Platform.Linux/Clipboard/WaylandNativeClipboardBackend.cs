namespace CrossMacro.Platform.Linux.Clipboard;

internal sealed class WaylandNativeClipboardBackend : INativeLinuxClipboardBackend
{
    private readonly SemaphoreSlim _ownerLock = new(1, 1);
    private WaylandClipboardConnection? _owner;
    private bool _disposed;

    public bool IsSupported { get; private set; }

    public string BackendName => "Wayland data-control/core (native libwayland-client)";

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await Task.Run(
                () =>
                {
                    using var connection = WaylandClipboardConnection.Connect(cancellationToken);
                    IsSupported = connection.IsSupported;
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or InvalidOperationException or IOException)
        {
            IsSupported = false;
            Log.Debug(ex, "[WaylandNativeClipboard] Native Wayland clipboard is unavailable");
        }
    }

    public async Task SetTextAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        await SetAsync(
            Encoding.UTF8.GetBytes(text),
            ["text/plain;charset=utf-8", "text/plain"],
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> GetTextAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsSupported)
        {
            throw new InvalidOperationException("The native Wayland clipboard backend is unavailable.");
        }

        return await WaylandClipboardConnection.ReadTextAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SetPngAsync(ReadOnlyMemory<byte> pngBytes, CancellationToken cancellationToken = default)
    {
        if (pngBytes.IsEmpty)
        {
            throw new ArgumentException("PNG clipboard data cannot be empty.", nameof(pngBytes));
        }

        await SetAsync(pngBytes.ToArray(), ["image/png"], cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Interlocked.Exchange(ref _owner, value: null)?.Dispose();
        _ownerLock.Dispose();
    }

    private async Task SetAsync(byte[] data, IReadOnlyList<string> mimeTypes, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsSupported)
        {
            throw new InvalidOperationException("The native Wayland clipboard backend is unavailable.");
        }

        await _ownerLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var nextOwner = await Task.Run(
                () =>
                {
                    var connection = WaylandClipboardConnection.Connect(cancellationToken);
                    try
                    {
                        connection.SetSelection(data, mimeTypes, cancellationToken);
                        connection.StartEventLoop();
                        return connection;
                    }
                    catch
                    {
                        connection.Dispose();
                        throw;
                    }
                },
                cancellationToken).ConfigureAwait(false);

            var previousOwner = Interlocked.Exchange(ref _owner, nextOwner);
            previousOwner?.Dispose();
        }
        finally
        {
            _ = _ownerLock.Release();
        }
    }
}
