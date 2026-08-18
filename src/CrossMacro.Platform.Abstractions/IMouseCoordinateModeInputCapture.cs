namespace CrossMacro.Platform.Abstractions;

/// <summary>
/// Allows a capture facade whose native event subscription depends on the
/// requested recording coordinate mode to select that subscription explicitly.
/// </summary>
public interface IMouseCoordinateModeInputCapture
{
    public void ConfigureCoordinateMode(
        bool useAbsoluteCoordinates,
        bool useLogicalCoordinates);
}
