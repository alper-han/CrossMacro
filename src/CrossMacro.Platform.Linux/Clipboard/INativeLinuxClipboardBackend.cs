namespace CrossMacro.Platform.Linux.Clipboard;

internal interface INativeLinuxClipboardBackend : IDisposable
{
    public bool IsSupported { get; }

    public string BackendName { get; }

    public Task InitializeAsync(CancellationToken cancellationToken = default);

    public Task SetTextAsync(string text, CancellationToken cancellationToken = default);

    public Task<string?> GetTextAsync(CancellationToken cancellationToken = default);

    public Task SetPngAsync(ReadOnlyMemory<byte> pngBytes, CancellationToken cancellationToken = default);
}
