
namespace CrossMacro.Core.Services;

/// <summary>
/// Abstraction for system clipboard interactions to remove external dependencies.
/// </summary>
public interface IClipboardService
{
    public bool IsSupported { get; }
    public Task SetTextAsync(string text, CancellationToken cancellationToken = default);
    public Task<string?> GetTextAsync(CancellationToken cancellationToken = default);
}
