namespace CrossMacro.Infrastructure.Services.Playback;

internal static class AbsoluteInputCoordinateMapper
{
    public static (int X, int Y) ToDeviceCoordinates(
        IInputSimulator simulator,
        ScreenRect? desktopBounds,
        int logicalX,
        int logicalY)
    {
        ArgumentNullException.ThrowIfNull(simulator);

        if (simulator is IInputSimulatorAbsoluteBounds { UsesZeroBasedScreenBounds: true }
            && desktopBounds is { } bounds)
        {
            return (
                checked(logicalX - bounds.X),
                checked(logicalY - bounds.Y));
        }

        return (logicalX, logicalY);
    }
}
