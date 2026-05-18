namespace CrossMacro.Core.Models;

/// <summary>
/// Coordinate interpretation for a coordinate-bearing mouse event.
/// </summary>
public enum MouseCoordinateMode
{
    /// <summary>
    /// Event coordinates are absolute screen coordinates.
    /// </summary>
    Absolute,

    /// <summary>
    /// Event coordinates are relative movement deltas.
    /// </summary>
    Relative
}
