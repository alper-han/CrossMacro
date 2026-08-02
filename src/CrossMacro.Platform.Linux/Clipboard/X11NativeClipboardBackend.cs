namespace CrossMacro.Platform.Linux.Clipboard;

internal sealed class X11NativeClipboardBackend : INativeLinuxClipboardBackend
{
    private readonly SemaphoreSlim _ownerLock = new(1, 1);
    private X11ClipboardOwner? _owner;
    private bool _disposed;

    public bool IsSupported { get; private set; }

    public string BackendName => "X11 Selection (native Xlib)";

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        var display = X11Native.XOpenDisplay(display: null);
        if (display == IntPtr.Zero)
        {
            IsSupported = false;
            return Task.CompletedTask;
        }

        _ = X11Native.XCloseDisplay(display);
        IsSupported = true;
        return Task.CompletedTask;
    }

    public async Task SetTextAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        await SetAsync(Encoding.UTF8.GetBytes(text), X11ClipboardDataKind.Text, cancellationToken).ConfigureAwait(false);
    }

    public Task<string?> GetTextAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsSupported)
        {
            throw new InvalidOperationException("The native X11 clipboard backend is unavailable.");
        }

        return Task.Run(() => X11ClipboardReader.ReadText(cancellationToken), cancellationToken);
    }

    public async Task SetPngAsync(ReadOnlyMemory<byte> pngBytes, CancellationToken cancellationToken = default)
    {
        if (pngBytes.IsEmpty)
        {
            throw new ArgumentException("PNG clipboard data cannot be empty.", nameof(pngBytes));
        }

        await SetAsync(pngBytes.ToArray(), X11ClipboardDataKind.Png, cancellationToken).ConfigureAwait(false);
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

    private async Task SetAsync(byte[] data, X11ClipboardDataKind dataKind, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsSupported)
        {
            throw new InvalidOperationException("The native X11 clipboard backend is unavailable.");
        }

        await _ownerLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var nextOwner = await Task.Run(
                () => X11ClipboardOwner.Create(data, dataKind, cancellationToken),
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
