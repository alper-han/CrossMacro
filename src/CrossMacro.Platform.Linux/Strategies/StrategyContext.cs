
namespace CrossMacro.Platform.Linux.Strategies;

public record StrategyContext(
    CompositorType Compositor,
    bool IsWayland,
    bool UseAbsoluteCoordinates,
    bool ForceRelative,
    bool SkipInitialZero,
    bool UseLogicalRelativeCoordinates
);
