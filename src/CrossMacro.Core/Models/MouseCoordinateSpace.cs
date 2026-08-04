namespace CrossMacro.Core.Models;

/// <summary>
/// Unit space used by coordinates carried by a mouse event.
/// </summary>
public enum MouseCoordinateSpace
{
    /// <summary>
    /// Coordinates and deltas are expressed in logical desktop pixels.
    /// </summary>
    LogicalDesktop,

    /// <summary>
    /// Relative deltas are unaccelerated values reported by an input device.
    /// </summary>
    RawDevice,
}
