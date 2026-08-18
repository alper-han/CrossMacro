
namespace CrossMacro.Platform.Linux.Strategies.Selectors;

public class WaylandAbsoluteStrategySelector(IMousePositionProvider positionProvider) : ICoordinateStrategySelector
{
    private readonly IMousePositionProvider _positionProvider = positionProvider;

    public int Priority => 10;

    public bool CanHandle(StrategyContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.IsWayland && context.UseAbsoluteCoordinates;
    }

    public ICoordinateStrategy Create(StrategyContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!_positionProvider.HasUsableAbsolutePosition())
        {
            Log.Warning(
                "[WaylandAbsoluteStrategySelector] Provider {ProviderName} has no usable cursor position for {Compositor}; falling back to raw relative strategy.",
                _positionProvider.ProviderName,
                context.Compositor);
            return new RelativeCoordinateStrategy();
        }

        return new CompositorCoordinateStrategy(
            _positionProvider,
            emitRelativeCoordinates: false);
    }
}
