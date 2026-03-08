using CrossMacro.Core.Services;
using CrossMacro.Core.Services.Recording.Strategies;
using CrossMacro.Platform.Linux.Strategies;
using Serilog;

namespace CrossMacro.Platform.Linux.Strategies.Selectors;

public class WaylandAbsoluteStrategySelector : ICoordinateStrategySelector
{
    private readonly IMousePositionProvider _positionProvider;

    public WaylandAbsoluteStrategySelector(IMousePositionProvider positionProvider)
    {
        _positionProvider = positionProvider;
    }

    public int Priority => 10;

    public bool CanHandle(StrategyContext context)
    {
        return context.IsWayland && context.UseAbsoluteCoordinates;
    }

    public ICoordinateStrategy Create(StrategyContext context)
    {
        if (!_positionProvider.IsSupported)
        {
            Log.Warning(
                "[WaylandAbsoluteStrategySelector] Provider {ProviderName} is unsupported for {Compositor}; falling back to relative strategy.",
                _positionProvider.ProviderName,
                context.Compositor);
            return new RelativeCoordinateStrategy();
        }

        return new EvdevAbsoluteStrategy(_positionProvider);
    }
}
