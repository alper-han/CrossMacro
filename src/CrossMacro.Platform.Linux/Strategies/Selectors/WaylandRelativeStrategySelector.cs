
namespace CrossMacro.Platform.Linux.Strategies.Selectors;

public class WaylandRelativeStrategySelector(IMousePositionProvider positionProvider) : ICoordinateStrategySelector
{
    private readonly IMousePositionProvider _positionProvider = positionProvider;

    public int Priority => 10;

    public bool CanHandle(StrategyContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.IsWayland && !context.UseAbsoluteCoordinates;
    }

    public ICoordinateStrategy Create(StrategyContext context)
    {
        if (_positionProvider.SupportsAbsolutePosition)
        {
            return new CompositorCoordinateStrategy(
                _positionProvider,
                emitRelativeCoordinates: true);
        }

        return new RelativeCoordinateStrategy();
    }
}
