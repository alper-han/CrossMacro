using System;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.MacOS.Strategies;

public class MacOSCoordinateStrategyFactory : ICoordinateStrategyFactory
{
    public MacOSCoordinateStrategyFactory()
    {
    }

    public ICoordinateStrategy Create(bool useAbsoluteCoordinates, bool forceRelative, bool skipInitialZero)
    {
        // macOS only supports absolute coordinates
        // forceRelative and skipInitialZero are ignored
        return new MacOSAbsoluteCoordinateStrategy();
    }
}
