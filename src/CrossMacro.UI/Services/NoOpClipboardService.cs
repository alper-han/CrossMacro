using System.Threading.Tasks;
using CrossMacro.Core.Services;

namespace CrossMacro.UI.Services;

/// <summary>
/// Clipboard fallback for non-GUI runtime contexts where no platform clipboard backend is available.
/// </summary>
public sealed class NoOpClipboardService : IClipboardService
{
    public bool IsSupported => false;

    public Task SetTextAsync(string text)
    {
        return Task.CompletedTask;
    }

    public Task<string?> GetTextAsync()
    {
        return Task.FromResult<string?>(null);
    }
}
