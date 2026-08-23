
namespace CrossMacro.Platform.Linux.Strategies;

public class LinuxCoordinateStrategyFactory(
    IEnumerable<ICoordinateStrategySelector> selectors,
    ILinuxEnvironmentDetector environmentDetector) : ICoordinateStrategyFactory
{
    private readonly IEnumerable<ICoordinateStrategySelector> _selectors = selectors;
    private readonly ILinuxEnvironmentDetector _environmentDetector = environmentDetector;

    public ICoordinateStrategy Create(bool useAbsoluteCoordinates, bool forceRelative, bool skipInitialZero) =>
        Create(useAbsoluteCoordinates, forceRelative, skipInitialZero, useLogicalRelativeCoordinates: false);

    public ICoordinateStrategy Create(bool useAbsoluteCoordinates, bool forceRelative, bool skipInitialZero, bool useLogicalRelativeCoordinates)
    {
        var compositor = _environmentDetector.DetectedCompositor;
        bool isWayland = _environmentDetector.IsWayland;

        var context = new StrategyContext(
            Compositor: compositor,
            IsWayland: isWayland,
            UseAbsoluteCoordinates: useAbsoluteCoordinates,
            ForceRelative: forceRelative,
            SkipInitialZero: skipInitialZero,
            UseLogicalRelativeCoordinates: useLogicalRelativeCoordinates
        );

        var strategy = _selectors
            .Where(s => s.CanHandle(context))
            .OrderByDescending(s => s.Priority)
            .FirstOrDefault()
            ?.Create(context);

        if (strategy is null)
        {
            // Fallback default if no selector matches (shouldn't happen with current selectors, but good for safety)
            // Default to Relative as it's the safest bet for macros
            return new RelativeCoordinateStrategy();
        }

        return strategy;
    }
}
