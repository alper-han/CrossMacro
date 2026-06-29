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

/// <summary>
/// Service for modifying window state (focus, close, move, resize, etc.).
/// </summary>
public interface IWindowMutationService
{
    /// <summary>Focuses the window with the given address. Returns true on success.</summary>
    Task<bool> FocusWindowByAddressAsync(string address, CancellationToken cancellationToken = default);

    /// <summary>Focuses the first window whose title contains titleSubstring. Returns true on success.</summary>
    Task<bool> FocusWindowByTitleAsync(string titleSubstring, CancellationToken cancellationToken = default);

    /// <summary>Focuses the first window whose class contains classSubstring. Returns true on success.</summary>
    Task<bool> FocusWindowByClassAsync(string classSubstring, CancellationToken cancellationToken = default);

    /// <summary>Closes (graceful) the window at the given address.</summary>
    Task<bool> CloseWindowByAddressAsync(string address, CancellationToken cancellationToken = default);

    /// <summary>Closes (graceful) the first window whose title contains titleSubstring.</summary>
    Task<bool> CloseWindowByTitleAsync(string titleSubstring, CancellationToken cancellationToken = default);

    /// <summary>Moves the currently active window to the given absolute pixel position.</summary>
    Task<bool> MoveActiveWindowAsync(int x, int y, CancellationToken cancellationToken = default);

    /// <summary>Resizes the currently active window to the given pixel dimensions.</summary>
    Task<bool> ResizeActiveWindowAsync(int width, int height, CancellationToken cancellationToken = default);

    /// <summary>Toggles fullscreen on the currently active window.</summary>
    Task<bool> FullscreenActiveWindowAsync(CancellationToken cancellationToken = default);

    /// <summary>Toggles floating mode on the currently active window.</summary>
    Task<bool> FloatActiveWindowAsync(CancellationToken cancellationToken = default);

    /// <summary>Centers the currently active window on its monitor.</summary>
    Task<bool> CenterActiveWindowAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for desktop and workspace management.
/// </summary>
public interface IWorkspaceManagementService
{
    /// <summary>Returns the name of the currently active workspace/desktop, or null if unavailable.</summary>
    Task<string?> GetActiveWorkspaceAsync(CancellationToken cancellationToken = default);

    /// <summary>Switches to the named workspace/desktop.</summary>
    Task<bool> SwitchWorkspaceAsync(string workspace, CancellationToken cancellationToken = default);

    /// <summary>Moves the currently active window to the named workspace/desktop.</summary>
    Task<bool> MoveActiveWindowToWorkspaceAsync(string workspace, CancellationToken cancellationToken = default);

    /// <summary>Moves the window at the given address to the named workspace/desktop.</summary>
    Task<bool> MoveWindowToWorkspaceByAddressAsync(string address, string workspace, CancellationToken cancellationToken = default);
}

/// <summary>
/// Composite interface for platforms that support all window operations.
/// </summary>
public interface IWindowManager : IWindowQueryService, IWindowMutationService, IWorkspaceManagementService
{
}
