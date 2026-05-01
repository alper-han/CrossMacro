using System.Threading;
using System.Threading.Tasks;

namespace CrossMacro.Platform.Abstractions;

/// <summary>
/// Service for capturing mouse coordinates and keyboard keys interactively.
/// Follows ISP by providing a focused interface for coordinate/key capture.
/// </summary>
public interface ICoordinateCaptureService
{
    /// <summary>
    /// Captures the current mouse position when user confirms (e.g., clicks or presses Enter).
    /// </summary>
    /// <param name="ct">Cancellation token to abort capture.</param>
    /// <returns>Tuple of (X, Y) coordinates, or null if cancelled.</returns>
    Task<(int X, int Y)?> CaptureMousePositionAsync(CancellationToken ct = default);

    /// <summary>
    /// Captures the next keyboard key pressed by the user.
    /// </summary>
    /// <param name="ct">Cancellation token to abort capture.</param>
    /// <returns>The key code, or null if cancelled.</returns>
    Task<int?> CaptureKeyCodeAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets whether a capture operation is currently in progress.
    /// </summary>
    bool IsCapturing { get; }

    /// <summary>
    /// Cancels any ongoing capture operation.
    /// </summary>
    void CancelCapture();
}
