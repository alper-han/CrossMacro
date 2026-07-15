
namespace CrossMacro.Platform.Abstractions;

/// <summary>
/// Service for querying window information.
/// </summary>
public interface IWindowQueryService
{
    /// <summary>Returns info about the currently active/focused window, or null if unavailable.</summary>
    public Task<WindowInfo?> GetActiveWindowAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns a list of all visible windows. Returns an empty list if unavailable.</summary>
    public Task<IReadOnlyList<WindowInfo>> GetWindowsAsync(CancellationToken cancellationToken = default);
}
