using CrossMacro.Infrastructure.Services.Recording.Strategies;
using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.Strategies;

namespace CrossMacro.Platform.Linux.Strategies.Selectors;

public class X11AbsoluteStrategySelector : ICoordinateStrategySelector
{
    private readonly IMousePositionProvider _positionProvider;

    public X11AbsoluteStrategySelector(IMousePositionProvider positionProvider)
    {
        _positionProvider = positionProvider;
    }

    public int Priority => 10;

    public bool CanHandle(StrategyContext context)
    {
        // Handle X11 explicitly or as fallback for non-Wayland
        return !context.IsWayland && context.UseAbsoluteCoordinates;
    }

    public ICoordinateStrategy Create(StrategyContext context)
    {
        return new AbsoluteCoordinateStrategy(_positionProvider);
    }
}
