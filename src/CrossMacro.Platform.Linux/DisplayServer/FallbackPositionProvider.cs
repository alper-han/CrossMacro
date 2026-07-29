
namespace CrossMacro.Platform.Linux.DisplayServer;

/// <summary>
/// Fallback position provider. Absolute tracking is not available; only relative
/// motion is supported when this provider is selected.
/// </summary>
public sealed class FallbackPositionProvider : IMousePositionProvider
{
    public string ProviderName => "None (Relative Only)";
    public bool IsSupported => false;

    public Task<(int X, int Y)?> GetAbsolutePositionAsync()
    {
        return Task.FromResult<(int X, int Y)?>(null);
    }

    public Task<(int Width, int Height)?> GetScreenResolutionAsync()
    {
        return Task.FromResult<(int Width, int Height)?>(null);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
