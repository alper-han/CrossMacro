using System;
using CrossMacro.Infrastructure.Services.Recording.Strategies;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.Linux.Strategies.Selectors;

public class X11RelativeStrategySelector : ICoordinateStrategySelector
{
    public int Priority => 10;

    public bool CanHandle(StrategyContext context)
    {
        // Handle X11 explicitly or as fallback for non-Wayland
        return !context.IsWayland && !context.UseAbsoluteCoordinates;
    }

    public ICoordinateStrategy Create(StrategyContext context)
    {
        return new RelativeCoordinateStrategy();
    }
}
