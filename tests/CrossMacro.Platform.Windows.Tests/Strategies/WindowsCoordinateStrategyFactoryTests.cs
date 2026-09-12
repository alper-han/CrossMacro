
namespace CrossMacro.Platform.Windows.Tests.Strategies;

public sealed class WindowsCoordinateStrategyFactoryTests
{
    [Fact]
    public void Constructor_WhenPositionProviderIsNull_ThrowsArgumentNullException()
    {
        var constructor = typeof(WindowsCoordinateStrategyFactory)
            .GetConstructors(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
            .Single();
        var invocation = Assert.Throws<System.Reflection.TargetInvocationException>(() => constructor.Invoke([null]));
        var argumentException = Assert.IsType<ArgumentNullException>(invocation.InnerException);
        Assert.Equal("positionProvider", argumentException.ParamName);
    }

    [WindowsFact]
    public void Create_WhenForceRelativeTrue_ReturnsRelativeStrategy()
    {
        var positionProvider = Substitute.For<IMousePositionProvider>();
        var factory = new WindowsCoordinateStrategyFactory(positionProvider);

        var strategy = factory.Create(useAbsoluteCoordinates: true, forceRelative: true, skipInitialZero: false);

        _ = Assert.IsType<CrossMacro.Platform.Abstractions.Recording.Strategies.RelativeCoordinateStrategy>(strategy);
        Assert.False(strategy.ProducesLogicalCoordinates);
    }

    [WindowsFact]
    public void Create_WhenLogicalRelativeRequested_ReturnsLogicalRelativeStrategy()
    {
        var positionProvider = Substitute.For<IMousePositionProvider>();
        var factory = new WindowsCoordinateStrategyFactory(positionProvider);

        var strategy = factory.Create(
            useAbsoluteCoordinates: true,
            forceRelative: true,
            skipInitialZero: false,
            useLogicalRelativeCoordinates: true);

        var relativeStrategy = Assert.IsType<CrossMacro.Platform.Abstractions.Recording.Strategies.RelativeCoordinateStrategy>(strategy);
        Assert.True(relativeStrategy.ProducesLogicalCoordinates);
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
        Assert.False(strategy.ProducesLogicalCoordinates);
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
