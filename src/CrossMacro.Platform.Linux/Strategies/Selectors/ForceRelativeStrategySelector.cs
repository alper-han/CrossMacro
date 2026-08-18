
namespace CrossMacro.Platform.Linux.Strategies.Selectors;

public class ForceRelativeStrategySelector(IMousePositionProvider positionProvider) : ICoordinateStrategySelector
{
    private readonly IMousePositionProvider _positionProvider = positionProvider;

    public int Priority => 100;

    public bool CanHandle(StrategyContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.ForceRelative;
    }

    public ICoordinateStrategy Create(StrategyContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.IsWayland && _positionProvider.HasUsableAbsolutePosition())
        {
            return new CompositorCoordinateStrategy(
                _positionProvider,
                emitRelativeCoordinates: true);
        }

        if (!context.IsWayland
            && context.Compositor is CompositorType.X11
            && _positionProvider.HasUsableAbsolutePosition())
        {
            return new X11LogicalRelativeCoordinateStrategy(_positionProvider);
        }

        return new RelativeCoordinateStrategy();
    }
}
