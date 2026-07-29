
namespace CrossMacro.Platform.Linux.Strategies.Selectors;

public class X11AbsoluteStrategySelector(IMousePositionProvider positionProvider) : ICoordinateStrategySelector
{
    private readonly IMousePositionProvider _positionProvider = positionProvider;

    public int Priority => 10;

    public bool CanHandle(StrategyContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        // Handle X11 explicitly or as fallback for non-Wayland
        return !context.IsWayland && context.UseAbsoluteCoordinates;
    }

    public ICoordinateStrategy Create(StrategyContext context)
    {
        return new AbsoluteCoordinateStrategy(_positionProvider);
    }
}
