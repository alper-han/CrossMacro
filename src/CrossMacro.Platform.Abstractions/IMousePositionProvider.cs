
namespace CrossMacro.Platform.Abstractions;

/// <summary>
/// Interface for mouse position providers across different platforms
/// </summary>
public interface IMousePositionProvider : IDisposable
{
    /// <summary>
    /// Name of the position provider (e.g., "Hyprland IPC", "KDE DBus")
    /// </summary>
    public string ProviderName { get; }

    /// <summary>
    /// Whether this provider is supported on the current system
    /// </summary>
    public bool IsSupported { get; }

    /// <summary>
    /// Whether this provider can return the global logical cursor position.
    /// Resolution-only providers override this with <see langword="false" />.
    /// </summary>
    public bool SupportsAbsolutePosition => IsSupported;

    /// <summary>
    /// Get the current absolute mouse position asynchronously
    /// </summary>
    /// <returns>Tuple of (X, Y) coordinates, or null if unavailable</returns>
    public Task<(int X, int Y)?> GetAbsolutePositionAsync();

    /// <summary>
    /// Get the screen resolution asynchronously
    /// </summary>
    /// <returns>Tuple of (Width, Height), or null if unavailable</returns>
    public Task<(int Width, int Height)?> GetScreenResolutionAsync();

    /// <summary>
    /// Gets the logical desktop bounds, including a non-zero origin for layouts
    /// with monitors positioned above or to the left of the primary display.
    /// </summary>
    public async Task<ScreenRect?> GetDesktopBoundsAsync()
    {
        var resolution = await GetScreenResolutionAsync().ConfigureAwait(false);
        if (resolution is null || resolution.Value.Width <= 0 || resolution.Value.Height <= 0)
        {
            return null;
        }

        return new ScreenRect(0, 0, resolution.Value.Width, resolution.Value.Height);
    }

    /// <summary>
    /// Task that completes when the provider is fully initialized
    /// </summary>
    public Task<bool> InitializationTask => Task.FromResult(true);
}
