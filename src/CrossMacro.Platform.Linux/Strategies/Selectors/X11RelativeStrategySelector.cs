
namespace CrossMacro.Platform.Linux.Strategies.Selectors;

public class X11RelativeStrategySelector(IMousePositionProvider positionProvider) : ICoordinateStrategySelector
{
    private readonly IMousePositionProvider _positionProvider =
        positionProvider ?? throw new ArgumentNullException(nameof(positionProvider));

    public int Priority => 10;

    public bool CanHandle(StrategyContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        // Handle X11 explicitly or as fallback for non-Wayland
        return !context.IsWayland && !context.UseAbsoluteCoordinates;
    }

    public ICoordinateStrategy Create(StrategyContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return _positionProvider.HasUsableAbsolutePosition()
            ? new X11LogicalRelativeCoordinateStrategy(_positionProvider)
            : new RelativeCoordinateStrategy();
    }
}
