
namespace CrossMacro.Platform.MacOS.Strategies;

public class MacOSCoordinateStrategyFactory : ICoordinateStrategyFactory
{
    private readonly IMousePositionProvider? _positionProvider;

    public MacOSCoordinateStrategyFactory() { /* Compatibility constructor for direct callers. */ }

    internal MacOSCoordinateStrategyFactory(IMousePositionProvider positionProvider)
    {
        _positionProvider = positionProvider ?? throw new ArgumentNullException(nameof(positionProvider));
    }

    public ICoordinateStrategy Create(bool useAbsoluteCoordinates, bool forceRelative, bool skipInitialZero) =>
        Create(useAbsoluteCoordinates, forceRelative, skipInitialZero, useLogicalRelativeCoordinates: false);

    public ICoordinateStrategy Create(bool useAbsoluteCoordinates, bool forceRelative, bool skipInitialZero, bool useLogicalRelativeCoordinates)
    {
        if (forceRelative && useLogicalRelativeCoordinates)
        {
            return _positionProvider is null
                ? new MacOSRelativeCoordinateStrategy()
                : new MacOSRelativeCoordinateStrategy(cancellationToken => _positionProvider
                    .GetAbsolutePositionAsync()
                    .WaitAsync(cancellationToken));
        }

        if (forceRelative || !useAbsoluteCoordinates)
        {
            return new RelativeCoordinateStrategy();
        }

        return _positionProvider is null
            ? new MacOSAbsoluteCoordinateStrategy()
            : new MacOSAbsoluteCoordinateStrategy(_positionProvider);
    }
}
