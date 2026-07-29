
namespace CrossMacro.Platform.MacOS.Tests.Strategies;

public sealed class MacOSCoordinateStrategyFactoryTests
{
    [Fact]
    public void Create_WhenAbsoluteRequested_ReturnsMacOSAbsoluteStrategy()
    {
        var factory = new MacOSCoordinateStrategyFactory();

        var strategy = factory.Create(useAbsoluteCoordinates: true, forceRelative: false, skipInitialZero: false);

        _ = Assert.IsType<MacOSAbsoluteCoordinateStrategy>(strategy);
    }

    [Fact]
    public void Create_WhenForceRelativeRequested_ReturnsMacOSRelativeStrategy()
    {
        var factory = new MacOSCoordinateStrategyFactory();

        var strategy = factory.Create(useAbsoluteCoordinates: true, forceRelative: true, skipInitialZero: false);

        _ = Assert.IsType<MacOSRelativeCoordinateStrategy>(strategy);
    }

    [Fact]
    public void Create_WhenRelativeRequested_ReturnsMacOSRelativeStrategy()
    {
        var factory = new MacOSCoordinateStrategyFactory();

        var strategy = factory.Create(useAbsoluteCoordinates: false, forceRelative: false, skipInitialZero: false);

        _ = Assert.IsType<MacOSRelativeCoordinateStrategy>(strategy);
    }

    [Fact]
    public void Create_WhenAbsoluteRequestedWithSkipInitialZero_ReturnsMacOSAbsoluteStrategy()
    {
        var factory = new MacOSCoordinateStrategyFactory();

        var strategy = factory.Create(useAbsoluteCoordinates: true, forceRelative: false, skipInitialZero: true);

        _ = Assert.IsType<MacOSAbsoluteCoordinateStrategy>(strategy);
    }
}
