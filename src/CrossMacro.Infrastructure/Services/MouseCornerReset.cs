namespace CrossMacro.Infrastructure.Services;

internal static class MouseCornerReset
{
    private const int MinimumRelativeResetDistance = 20_000;

    public static (int X, int Y)? MoveToDesktopOrigin(
        IInputSimulator simulator,
        ScreenRect? desktopBounds)
    {
        ArgumentNullException.ThrowIfNull(simulator);

        if (desktopBounds is { Width: > 0, Height: > 0 } bounds
            && simulator is IInputSimulatorCapabilities { SupportsAbsoluteCoordinates: true })
        {
            var absoluteBounds = simulator as IInputSimulatorAbsoluteBounds;
            bool usesZeroBasedBounds = absoluteBounds?.UsesZeroBasedScreenBounds is true;
            if (usesZeroBasedBounds)
            {
                simulator.MoveAbsolute(
                    bounds.Width > 1 ? 1 : 0,
                    bounds.Height > 1 ? 1 : 0);
                simulator.MoveAbsolute(0, 0);
            }
            else
            {
                simulator.MoveAbsolute(bounds.X, bounds.Y);
            }

            return (bounds.X, bounds.Y);
        }

        int horizontalDistance = ResolveRelativeResetDistance(desktopBounds?.Width);
        int verticalDistance = ResolveRelativeResetDistance(desktopBounds?.Height);
        MoveWithAxisSeparatedRelativeFallback(simulator, horizontalDistance, verticalDistance);
        return null;
    }

    private static void MoveWithAxisSeparatedRelativeFallback(
        IInputSimulator simulator,
        int horizontalDistance,
        int verticalDistance)
    {
        simulator.MoveRelative(-horizontalDistance, 0);
        simulator.MoveRelative(0, -verticalDistance);
        simulator.MoveRelative(-horizontalDistance, 0);
        simulator.MoveRelative(0, -verticalDistance);
    }

    private static int ResolveRelativeResetDistance(int? desktopExtent)
    {
        long extent = desktopExtent.GetValueOrDefault();
        long resetDistance = Math.Max(MinimumRelativeResetDistance, extent * 2L);
        return (int)Math.Min(resetDistance, int.MaxValue);
    }
}
