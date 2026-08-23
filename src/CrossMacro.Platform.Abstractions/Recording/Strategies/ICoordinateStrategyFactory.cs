namespace CrossMacro.Platform.Abstractions;

public interface ICoordinateStrategyFactory
{
    public ICoordinateStrategy Create(bool useAbsoluteCoordinates, bool forceRelative, bool skipInitialZero);

    public ICoordinateStrategy Create(
        bool useAbsoluteCoordinates,
        bool forceRelative,
        bool skipInitialZero,
        bool useLogicalRelativeCoordinates) => Create(
            useAbsoluteCoordinates,
            forceRelative,
            skipInitialZero);
}
