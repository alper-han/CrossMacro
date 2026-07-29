
namespace CrossMacro.Core.Services.Playback;

/// <summary>
/// Interface for executing input events (mouse, keyboard).
/// Implementations compose IInputSimulator with state trackers.
/// </summary>
public interface IEventExecutor : IDisposable
{
    /// <summary>
    /// Initialize the executor (create virtual device)
    /// </summary>
    public void Initialize(int screenWidth, int screenHeight);

    /// <summary>
    /// Move mouse to absolute position
    /// </summary>
    public void MoveAbsolute(int x, int y);

    /// <summary>
    /// Move mouse by relative delta
    /// </summary>
    public void MoveRelative(int dx, int dy);

    /// <summary>
    /// Press or release mouse button
    /// </summary>
    public void EmitButton(ushort button, bool pressed);

    /// <summary>
    /// Emit mouse scroll
    /// </summary>
    public void EmitScroll(int value);

    /// <summary>
    /// Press or release keyboard key
    /// </summary>
    public void EmitKey(int code, bool pressed);

    /// <summary>
    /// Release all pressed inputs (safety)
    /// </summary>
    public void ReleaseAll();

    /// <summary>
    /// Whether any mouse button is currently pressed
    /// </summary>
    public bool IsMouseButtonPressed { get; }

    /// <summary>
    /// Execute a macro event with full handling. A null coordinate mode means no implicit coordinate movement.
    /// </summary>
    public void Execute(MacroEvent ev, MouseCoordinateMode? coordinateMode);
}
