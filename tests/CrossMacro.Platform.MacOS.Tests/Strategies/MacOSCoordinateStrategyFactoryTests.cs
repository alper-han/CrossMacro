using CrossMacro.Platform.MacOS.Strategies;
using Xunit;

namespace CrossMacro.Platform.MacOS.Tests.Strategies;

public class MacOSCoordinateStrategyFactoryTests
{
    [Fact]
    public void Create_WhenAbsoluteRequested_ReturnsMacOSAbsoluteStrategy()
    {
        var factory = new MacOSCoordinateStrategyFactory();

        var strategy = factory.Create(useAbsoluteCoordinates: true, forceRelative: false, skipInitialZero: false);

        Assert.IsType<MacOSAbsoluteCoordinateStrategy>(strategy);
    }

    [Fact]
    public void Create_WhenForceRelativeRequested_ReturnsMacOSRelativeStrategy()
    {
        var factory = new MacOSCoordinateStrategyFactory();

        var strategy = factory.Create(useAbsoluteCoordinates: true, forceRelative: true, skipInitialZero: false);

        Assert.IsType<MacOSRelativeCoordinateStrategy>(strategy);
    }

    [Fact]
    public void Create_WhenRelativeRequested_ReturnsMacOSRelativeStrategy()
    {
        var factory = new MacOSCoordinateStrategyFactory();

        var strategy = factory.Create(useAbsoluteCoordinates: false, forceRelative: false, skipInitialZero: false);

        Assert.IsType<MacOSRelativeCoordinateStrategy>(strategy);
    }

    [Fact]
    public void Create_WhenAbsoluteRequestedWithSkipInitialZero_ReturnsMacOSAbsoluteStrategy()
    {
        var factory = new MacOSCoordinateStrategyFactory();

        var strategy = factory.Create(useAbsoluteCoordinates: true, forceRelative: false, skipInitialZero: true);

        Assert.IsType<MacOSAbsoluteCoordinateStrategy>(strategy);
    }
}
