using System.Threading.Tasks;

namespace CrossMacro.Core.Services;

/// <summary>
/// Abstraction for system clipboard interactions to remove external dependencies.
/// </summary>
public interface IClipboardService
{
    Task SetTextAsync(string text);
    Task<string?> GetTextAsync();
}
