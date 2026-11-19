namespace CrossMacro.Platform.Abstractions.Recording.Strategies;

public interface ICoordinateStrategyFactory
{
    public ICoordinateStrategy Create(
        bool useAbsoluteCoordinates,
        bool forceRelative,
        bool skipInitialZero,
        bool useLogicalRelativeCoordinates);
}
