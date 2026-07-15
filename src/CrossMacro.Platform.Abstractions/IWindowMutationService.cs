using System.Threading;
using System.Threading.Tasks;

namespace CrossMacro.Platform.Abstractions;

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

    /// <summary>Toggles maximized state on the currently active window.</summary>
    Task<bool> MaximizeActiveWindowAsync(CancellationToken cancellationToken = default);

    /// <summary>Toggles floating mode on the currently active window.</summary>
    Task<bool> FloatActiveWindowAsync(CancellationToken cancellationToken = default);

    /// <summary>Centers the currently active window on its monitor.</summary>
    Task<bool> CenterActiveWindowAsync(CancellationToken cancellationToken = default);
}
