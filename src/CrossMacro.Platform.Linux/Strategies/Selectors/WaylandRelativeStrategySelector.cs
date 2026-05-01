using System;
using CrossMacro.Infrastructure.Services.Recording.Strategies;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.Linux.Strategies.Selectors;

public class WaylandRelativeStrategySelector : ICoordinateStrategySelector
{
    public int Priority => 10;

    public bool CanHandle(StrategyContext context)
    {
        return context.IsWayland && !context.UseAbsoluteCoordinates;
    }

    public ICoordinateStrategy Create(StrategyContext context)
    {
        return new RelativeCoordinateStrategy();
    }
}
