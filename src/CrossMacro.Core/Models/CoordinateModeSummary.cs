namespace CrossMacro.Core.Models;

/// <summary>
/// Aggregate coordinate mode classification for a macro sequence.
/// </summary>
public enum CoordinateModeSummary
{
    /// <summary>
    /// The macro contains no coordinate-bearing events.
    /// </summary>
    None,

    /// <summary>
    /// All coordinate-bearing events resolve to absolute coordinates.
    /// </summary>
    Absolute,

    /// <summary>
    /// All coordinate-bearing events resolve to relative deltas.
    /// </summary>
    Relative,

    /// <summary>
    /// Coordinate-bearing events resolve to both absolute coordinates and relative deltas.
    /// </summary>
    Mixed
}
