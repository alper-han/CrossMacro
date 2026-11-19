using CrossMacro.Platform.Abstractions.Recording.Strategies;

namespace CrossMacro.Platform.MacOS.Strategies;

internal sealed class MacOSCoordinateStrategyFactory(IMousePositionProvider positionProvider) : ICoordinateStrategyFactory
{
    private readonly IMousePositionProvider _positionProvider = positionProvider ?? throw new ArgumentNullException(nameof(positionProvider));

    public ICoordinateStrategy Create(bool useAbsoluteCoordinates, bool forceRelative, bool skipInitialZero) =>
        Create(useAbsoluteCoordinates, forceRelative, skipInitialZero, useLogicalRelativeCoordinates: false);

    public ICoordinateStrategy Create(bool useAbsoluteCoordinates, bool forceRelative, bool skipInitialZero, bool useLogicalRelativeCoordinates)
    {
        if (forceRelative && useLogicalRelativeCoordinates)
        {
            return new MacOSRelativeCoordinateStrategy(_positionProvider);
        }

        if (forceRelative || !useAbsoluteCoordinates)
        {
            return new RelativeCoordinateStrategy();
        }

        return new MacOSAbsoluteCoordinateStrategy(_positionProvider);
    }
}
