namespace CrossMacro.Platform.Abstractions;

/// <summary>
/// Describes the coordinate bounds applied by an absolute input transport.
/// </summary>
public interface IInputSimulatorAbsoluteBounds
{
    /// <summary>
    /// Gets whether absolute coordinates are clamped to the zero-based screen dimensions
    /// supplied when the simulator is initialized.
    /// </summary>
    public bool UsesZeroBasedScreenBounds { get; }
}
