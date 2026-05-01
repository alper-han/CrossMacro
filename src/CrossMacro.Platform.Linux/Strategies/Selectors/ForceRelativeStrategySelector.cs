using System;
using CrossMacro.Infrastructure.Services.Recording.Strategies;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.Linux.Strategies.Selectors;

public class ForceRelativeStrategySelector : ICoordinateStrategySelector
{
    public int Priority => 100;

    public bool CanHandle(StrategyContext context)
    {
        return context.ForceRelative;
    }

    public ICoordinateStrategy Create(StrategyContext context)
    {
        return new RelativeCoordinateStrategy();
    }
}
