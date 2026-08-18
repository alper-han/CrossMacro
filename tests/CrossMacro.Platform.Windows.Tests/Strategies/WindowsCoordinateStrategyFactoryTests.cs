
namespace CrossMacro.Platform.Windows.Tests.Strategies;

public sealed class WindowsCoordinateStrategyFactoryTests
{
    [WindowsFact]
    public void Create_WhenForceRelativeTrue_ReturnsRelativeStrategy()
    {
        var positionProvider = Substitute.For<IMousePositionProvider>();
        var factory = new WindowsCoordinateStrategyFactory(positionProvider);

        var strategy = factory.Create(useAbsoluteCoordinates: true, forceRelative: true, skipInitialZero: false);

        _ = Assert.IsType<CrossMacro.Platform.Abstractions.Recording.Strategies.RelativeCoordinateStrategy>(strategy);
    }

    [WindowsFact]
    public void Create_WhenAbsoluteRequested_ReturnsWindowsAbsoluteStrategy()
    {
        var positionProvider = Substitute.For<IMousePositionProvider>();
        var factory = new WindowsCoordinateStrategyFactory(positionProvider);

        var strategy = factory.Create(useAbsoluteCoordinates: true, forceRelative: false, skipInitialZero: false);

        _ = Assert.IsType<WindowsAbsoluteCoordinateStrategy>(strategy);
    }

    [WindowsFact]
    public void Create_WhenRelativeRequested_ReturnsRelativeStrategy()
    {
        var positionProvider = Substitute.For<IMousePositionProvider>();
        var factory = new WindowsCoordinateStrategyFactory(positionProvider);

        var strategy = factory.Create(useAbsoluteCoordinates: false, forceRelative: false, skipInitialZero: false);

        _ = Assert.IsType<CrossMacro.Platform.Abstractions.Recording.Strategies.RelativeCoordinateStrategy>(strategy);
    }

    [WindowsFact]
    public void Create_WhenSkipInitialZeroTrue_DoesNotChangeWindowsDecision()
    {
        var positionProvider = Substitute.For<IMousePositionProvider>();
        var factory = new WindowsCoordinateStrategyFactory(positionProvider);

        var strategy = factory.Create(useAbsoluteCoordinates: true, forceRelative: false, skipInitialZero: true);

        _ = Assert.IsType<WindowsAbsoluteCoordinateStrategy>(strategy);
    }
}
