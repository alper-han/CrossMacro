
namespace CrossMacro.Platform.Windows.Strategies;

public class WindowsCoordinateStrategyFactory(
    IMousePositionProvider positionProvider) : ICoordinateStrategyFactory
{
    private readonly IMousePositionProvider _positionProvider = positionProvider;

    public ICoordinateStrategy Create(bool useAbsoluteCoordinates, bool forceRelative, bool skipInitialZero) =>
        Create(useAbsoluteCoordinates, forceRelative, skipInitialZero, useLogicalRelativeCoordinates: false);

    public ICoordinateStrategy Create(bool useAbsoluteCoordinates, bool forceRelative, bool skipInitialZero, bool useLogicalRelativeCoordinates)
    {
        if (forceRelative)
        {
            return new RelativeCoordinateStrategy(producesLogicalCoordinates: useLogicalRelativeCoordinates);
        }

        if (useAbsoluteCoordinates)
        {
            return new WindowsAbsoluteCoordinateStrategy(_positionProvider);
        }

        return new RelativeCoordinateStrategy();
    }
}
