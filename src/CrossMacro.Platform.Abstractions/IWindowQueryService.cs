using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CrossMacro.Platform.Abstractions;

/// <summary>
/// Service for querying window information.
/// </summary>
public interface IWindowQueryService
{
    /// <summary>Returns info about the currently active/focused window, or null if unavailable.</summary>
    Task<WindowInfo?> GetActiveWindowAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns a list of all visible windows. Returns an empty list if unavailable.</summary>
    Task<IReadOnlyList<WindowInfo>> GetWindowsAsync(CancellationToken cancellationToken = default);
}
