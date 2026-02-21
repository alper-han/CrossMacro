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
    public void Create_WhenForceRelativeRequested_StillReturnsMacOSAbsoluteStrategy()
    {
        var factory = new MacOSCoordinateStrategyFactory();

        var strategy = factory.Create(useAbsoluteCoordinates: false, forceRelative: true, skipInitialZero: false);

        Assert.IsType<MacOSAbsoluteCoordinateStrategy>(strategy);
    }

    [Fact]
    public void Create_WhenSkipInitialZeroRequested_StillReturnsMacOSAbsoluteStrategy()
    {
        var factory = new MacOSCoordinateStrategyFactory();

        var strategy = factory.Create(useAbsoluteCoordinates: false, forceRelative: false, skipInitialZero: true);

        Assert.IsType<MacOSAbsoluteCoordinateStrategy>(strategy);
    }
}
