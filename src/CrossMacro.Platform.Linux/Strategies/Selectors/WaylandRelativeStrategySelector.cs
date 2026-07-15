
namespace CrossMacro.Platform.Linux.Strategies.Selectors;

public class WaylandRelativeStrategySelector : ICoordinateStrategySelector
{
    public int Priority => 10;

    public bool CanHandle(StrategyContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.IsWayland && !context.UseAbsoluteCoordinates;
    }

    public ICoordinateStrategy Create(StrategyContext context)
    {
        return new RelativeCoordinateStrategy();
    }
}
