using CrossMacro.Core.Services;
using CrossMacro.Core.Services.Recording.Strategies;
using CrossMacro.Platform.Windows.Strategies;
using CrossMacro.TestInfrastructure;
using NSubstitute;
using Xunit;

namespace CrossMacro.Platform.Windows.Tests.Strategies;

public class WindowsCoordinateStrategyFactoryTests
{
    [WindowsFact]
    public void Create_WhenForceRelativeTrue_ReturnsRelativeStrategy()
    {
        var positionProvider = Substitute.For<IMousePositionProvider>();
        var factory = new WindowsCoordinateStrategyFactory(positionProvider);

        var strategy = factory.Create(useAbsoluteCoordinates: true, forceRelative: true, skipInitialZero: false);

        Assert.IsType<RelativeCoordinateStrategy>(strategy);
    }

    [WindowsFact]
    public void Create_WhenAbsoluteRequested_ReturnsWindowsAbsoluteStrategy()
    {
        var positionProvider = Substitute.For<IMousePositionProvider>();
        var factory = new WindowsCoordinateStrategyFactory(positionProvider);

        var strategy = factory.Create(useAbsoluteCoordinates: true, forceRelative: false, skipInitialZero: false);

        Assert.IsType<WindowsAbsoluteCoordinateStrategy>(strategy);
    }

    [WindowsFact]
    public void Create_WhenRelativeRequested_ReturnsRelativeStrategy()
    {
        var positionProvider = Substitute.For<IMousePositionProvider>();
        var factory = new WindowsCoordinateStrategyFactory(positionProvider);

        var strategy = factory.Create(useAbsoluteCoordinates: false, forceRelative: false, skipInitialZero: false);

        Assert.IsType<RelativeCoordinateStrategy>(strategy);
    }

    [WindowsFact]
    public void Create_WhenSkipInitialZeroTrue_DoesNotChangeWindowsDecision()
    {
        var positionProvider = Substitute.For<IMousePositionProvider>();
        var factory = new WindowsCoordinateStrategyFactory(positionProvider);

        var strategy = factory.Create(useAbsoluteCoordinates: true, forceRelative: false, skipInitialZero: true);

        Assert.IsType<WindowsAbsoluteCoordinateStrategy>(strategy);
    }
}
