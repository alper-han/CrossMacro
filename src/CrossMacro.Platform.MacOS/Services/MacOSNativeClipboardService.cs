namespace CrossMacro.Platform.MacOS.Services;

[SupportedOSPlatform("macos")]
internal sealed class MacOSNativeClipboardService :
    IClipboardService,
    IImageClipboardService,
    IImageClipboardReader,
    IDisposable
{
    private readonly IMacOSClipboardBackend _backend;
    private readonly Func<bool> _isMacOS;
    private readonly SemaphoreSlim _clipboardLock = new(1, 1);
    private int _disposed;

    public MacOSNativeClipboardService()
        : this(new MacOSPasteboardBackend()) { /* Empty */ }

    internal MacOSNativeClipboardService(IMacOSClipboardBackend backend, Func<bool>? isMacOS = null)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _isMacOS = isMacOS ?? OperatingSystem.IsMacOS;
    }

    public bool IsSupported => Volatile.Read(ref _disposed) is 0 && _isMacOS() && _backend.IsAvailable;

    public async Task SetTextAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        await UseClipboardAsync(
            () => _backend.TrySetText(text),
            "Failed to write text to the macOS clipboard.",
            imageOperation: false,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> GetTextAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _clipboardLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureSupported(imageOperation: false);
            return _backend.GetText();
        }
        finally
        {
            _ = _clipboardLock.Release();
        }
    }

    public async Task SetPngAsync(ReadOnlyMemory<byte> pngBytes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        if (pngBytes.IsEmpty)
        {
            return;
        }

        var pngArray = pngBytes.ToArray();
        await UseClipboardAsync(
            () => _backend.TrySetPng(pngArray),
            "Failed to write PNG data to the macOS clipboard.",
            imageOperation: true,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<byte[]?> GetPngAsync(int maximumBytes, CancellationToken cancellationToken = default)
    {
        if (maximumBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumBytes), "Maximum PNG bytes must be positive.");
        }

        ThrowIfDisposed();
        await _clipboardLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureSupported(imageOperation: true);
            return _backend.GetPng(maximumBytes);
        }
        finally
        {
            _ = _clipboardLock.Release();
        }
    }

    private async Task UseClipboardAsync(
        Func<bool> operation,
        string errorMessage,
        bool imageOperation,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _clipboardLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureSupported(imageOperation);
            if (!operation())
            {
                throw new InvalidOperationException(errorMessage);
            }
        }
        finally
        {
            _ = _clipboardLock.Release();
        }
    }

    private void EnsureSupported(bool imageOperation)
    {
        if (!_isMacOS() || !_backend.IsAvailable)
        {
            const string message = "macOS NSPasteboard is unavailable in this runtime.";
            if (imageOperation)
            {
                throw new ImageClipboardUnavailableException(message);
            }

            throw new InvalidOperationException(message);
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) is not 0, this);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) is not 0)
        {
            return;
        }

        // Do not dispose the semaphore while operations that started before disposal may still be queued.
    }
}
