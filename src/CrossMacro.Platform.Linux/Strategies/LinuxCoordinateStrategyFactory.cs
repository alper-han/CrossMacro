namespace CrossMacro.Platform.Linux.Strategies;

/// <summary>
/// Picks the coordinate strategy for a recording from the session type, the requested coordinate mode
/// and whether the active compositor can report the global cursor position.
/// </summary>
public sealed class LinuxCoordinateStrategyFactory(
    ILinuxEnvironmentDetector environmentDetector,
    IMousePositionProvider positionProvider) : ICoordinateStrategyFactory
{
    private readonly ILinuxEnvironmentDetector _environmentDetector =
        environmentDetector ?? throw new ArgumentNullException(nameof(environmentDetector));
    private readonly IMousePositionProvider _positionProvider =
        positionProvider ?? throw new ArgumentNullException(nameof(positionProvider));

    public ICoordinateStrategy Create(bool useAbsoluteCoordinates, bool forceRelative, bool skipInitialZero) =>
        Create(useAbsoluteCoordinates, forceRelative, skipInitialZero, useLogicalRelativeCoordinates: false);

    public ICoordinateStrategy Create(bool useAbsoluteCoordinates, bool forceRelative, bool skipInitialZero, bool useLogicalRelativeCoordinates)
    {
        var compositor = _environmentDetector.DetectedCompositor;
        var isWayland = _environmentDetector.IsWayland;

        if (forceRelative)
        {
            return CreateForcedRelative(compositor, isWayland, useLogicalRelativeCoordinates);
        }

        return (isWayland, useAbsoluteCoordinates) switch
        {
            (true, true) => CreateWaylandAbsolute(compositor),
            (true, false) => CreateWaylandRelative(),
            (false, true) => new AbsoluteCoordinateStrategy(_positionProvider),
            (false, false) => CreateX11Relative(),
        };
    }

    private ICoordinateStrategy CreateForcedRelative(CompositorType compositor, bool isWayland, bool useLogicalRelativeCoordinates)
    {
        if (!useLogicalRelativeCoordinates)
        {
            return new RelativeCoordinateStrategy();
        }

        if (!_positionProvider.HasUsableAbsolutePosition())
        {
            throw new InvalidOperationException("Logical relative recording requires access to the global cursor position.");
        }

        if (isWayland)
        {
            return new CompositorCoordinateStrategy(
                _positionProvider,
                emitRelativeCoordinates: true,
                allowRawRelativeFallback: false);
        }

        return compositor is CompositorType.X11
            ? new X11LogicalRelativeCoordinateStrategy(_positionProvider)
            : throw new InvalidOperationException("Logical relative recording is not supported by the active input backend.");
    }

    private ICoordinateStrategy CreateWaylandAbsolute(CompositorType compositor)
    {
        if (!_positionProvider.HasUsableAbsolutePosition())
        {
            Log.Warning(
                "[LinuxCoordinateStrategyFactory] Provider {ProviderName} has no usable cursor position for {Compositor}; falling back to raw relative strategy.",
                _positionProvider.ProviderName,
                compositor);
            return new RelativeCoordinateStrategy();
        }

        return new CompositorCoordinateStrategy(_positionProvider, emitRelativeCoordinates: false);
    }

    private ICoordinateStrategy CreateWaylandRelative() =>
        _positionProvider.HasUsableAbsolutePosition()
            ? new CompositorCoordinateStrategy(_positionProvider, emitRelativeCoordinates: true)
            : new RelativeCoordinateStrategy();

    private ICoordinateStrategy CreateX11Relative() =>
        _positionProvider.HasUsableAbsolutePosition()
            ? new X11LogicalRelativeCoordinateStrategy(_positionProvider)
            : new RelativeCoordinateStrategy();
}
