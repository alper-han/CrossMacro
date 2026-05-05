using System.Threading;
using System.Threading.Tasks;

namespace CrossMacro.Core.Services;

/// <summary>
/// Abstraction for system clipboard interactions to remove external dependencies.
/// </summary>
public interface IClipboardService
{
    bool IsSupported { get; }
    Task SetTextAsync(string text, CancellationToken cancellationToken = default);
    Task<string?> GetTextAsync(CancellationToken cancellationToken = default);
}
