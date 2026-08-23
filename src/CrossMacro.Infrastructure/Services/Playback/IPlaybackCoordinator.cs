
namespace CrossMacro.Infrastructure.Services.Playback;

/// <summary>
/// Handles playback initialization and per-iteration setup.
/// Platform-specific implementations handle Corner Reset, position sync, etc.
/// </summary>
public interface IPlaybackCoordinator
{
    /// <summary>
    /// Configures logical desktop bounds used when cursor synchronization is unavailable.
    /// </summary>
    public void ConfigureDesktopBounds(ScreenRect? desktopBounds);

    /// <summary>
    /// Initialize playback for a macro (called once at start)
    /// </summary>
    /// <param name="macro">The macro being played</param>
    /// <param name="simulator">Input simulator to use</param>
    /// <param name="screenWidth">Screen width (0 if unknown)</param>
    /// <param name="screenHeight">Screen height (0 if unknown)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public Task InitializeAsync(
        MacroSequence macro,
        IInputSimulator simulator,
        int screenWidth,
        int screenHeight,
        CancellationToken cancellationToken);

    /// <summary>
    /// Prepare for a new iteration (called before each loop)
    /// </summary>
    /// <param name="iteration">Current iteration number (0-based)</param>
    /// <param name="macro">The macro being played</param>
    /// <param name="simulator">Input simulator to use</param>
    /// <param name="screenWidth">Screen width (0 if unknown)</param>
    /// <param name="screenHeight">Screen height (0 if unknown)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public Task PrepareIterationAsync(
        int iteration,
        MacroSequence macro,
        IInputSimulator simulator,
        int screenWidth,
        int screenHeight,
        CancellationToken cancellationToken);

    /// <summary>
    /// Current X position (tracked internally)
    /// </summary>
    public int CurrentX { get; }

    /// <summary>
    /// Current Y position (tracked internally)
    /// </summary>
    public int CurrentY { get; }

    /// <summary>
    /// Whether the tracked coordinates are known to match the logical cursor position.
    /// </summary>
    public bool HasKnownPosition { get; }

    /// <summary>
    /// Refreshes the logical cursor position when the current estimate is unknown.
    /// </summary>
    public Task<bool> TrySynchronizePositionAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Refreshes the logical cursor position even when a prior estimate exists.
    /// This is used by cooperative logical-relative movement so manual cursor
    /// movement becomes the next delta's origin.
    /// </summary>
    public Task<bool> RefreshPositionAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Waits until the compositor reports the requested absolute position after
    /// an injected move. Input injection acknowledgement only confirms that the
    /// event reached the virtual device; it does not guarantee that the
    /// compositor has applied the pointer update before a following click.
    /// This verifies delivery but does not rebase the command position.
    /// </summary>
    public Task<bool> WaitForPositionAsync(int expectedX, int expectedY, CancellationToken cancellationToken);

    /// <summary>
    /// Update tracked position
    /// </summary>
    public void UpdatePosition(int x, int y);

    /// <summary>
    /// Marks the tracked position unknown. Set <paramref name="movementMayBePending" /> when
    /// a raw movement was just injected and the platform position provider may still expose
    /// the pre-injection position briefly.
    /// </summary>
    public void InvalidatePosition(bool movementMayBePending = false);
}
