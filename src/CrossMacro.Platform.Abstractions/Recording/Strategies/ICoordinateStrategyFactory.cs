namespace CrossMacro.Platform.Abstractions;

public interface ICoordinateStrategyFactory
{
    ICoordinateStrategy Create(bool useAbsoluteCoordinates, bool forceRelative, bool skipInitialZero);
}
