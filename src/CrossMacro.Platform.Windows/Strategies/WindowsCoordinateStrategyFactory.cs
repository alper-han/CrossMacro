
namespace CrossMacro.Platform.Windows.Strategies;

public class WindowsCoordinateStrategyFactory(
    IMousePositionProvider positionProvider) : ICoordinateStrategyFactory
{
    private readonly IMousePositionProvider _positionProvider = positionProvider;

    public ICoordinateStrategy Create(bool useAbsoluteCoordinates, bool forceRelative, bool skipInitialZero)
    {
        if (forceRelative)
        {
            return new RelativeCoordinateStrategy(producesLogicalCoordinates: true);
        }

        if (useAbsoluteCoordinates)
        {
            return new WindowsAbsoluteCoordinateStrategy(_positionProvider);
        }

        return new RelativeCoordinateStrategy(producesLogicalCoordinates: true);
    }
}
