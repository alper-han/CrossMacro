using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.MacOS.Strategies;

public class MacOSCoordinateStrategyFactory : ICoordinateStrategyFactory
{
    public MacOSCoordinateStrategyFactory()
    {
    }

    public ICoordinateStrategy Create(bool useAbsoluteCoordinates, bool forceRelative, bool skipInitialZero)
    {
        if (forceRelative || !useAbsoluteCoordinates)
        {
            return new MacOSRelativeCoordinateStrategy();
        }

        return new MacOSAbsoluteCoordinateStrategy();
    }
}
