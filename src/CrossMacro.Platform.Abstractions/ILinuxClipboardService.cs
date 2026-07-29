namespace CrossMacro.Platform.Abstractions;

public interface ILinuxClipboardService
{
    public bool IsSupported { get; }
    public Task SetTextAsync(string text, CancellationToken cancellationToken = default);
    public Task<string?> GetTextAsync(CancellationToken cancellationToken = default);
    public Task InitializeAsync(CancellationToken cancellationToken = default);
}
