
namespace CrossMacro.Infrastructure.Services.Playback;

/// <summary>
/// Handles playback initialization and per-iteration setup.
/// Platform-specific implementations handle Corner Reset, position sync, etc.
/// </summary>
public interface IPlaybackCoordinator
{
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
    /// Update tracked position
    /// </summary>
    public void UpdatePosition(int x, int y);

    /// <summary>
    /// Add delta to tracked position
    /// </summary>
    public void AddDelta(int dx, int dy);
}
