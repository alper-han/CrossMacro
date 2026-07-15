
namespace CrossMacro.Platform.Linux.Strategies;

public record class StrategyContext(
    CompositorType Compositor,
    bool IsWayland,
    bool UseAbsoluteCoordinates,
    bool ForceRelative,
    bool SkipInitialZero
);
