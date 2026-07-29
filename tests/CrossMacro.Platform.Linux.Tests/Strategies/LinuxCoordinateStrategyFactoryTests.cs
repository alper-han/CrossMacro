
namespace CrossMacro.Platform.Linux.Tests.Strategies;

public sealed class LinuxCoordinateStrategyFactoryTests
{
    private readonly IMousePositionProvider _mockPositionProvider;
    private readonly Func<IInputSimulator> _mockInputSimulatorFactory;
    private readonly ILinuxEnvironmentDetector _mockEnvironmentDetector;
    private readonly List<ICoordinateStrategySelector> _selectors;
    private readonly LinuxCoordinateStrategyFactory _factory;

    public LinuxCoordinateStrategyFactoryTests()
    {
        _mockPositionProvider = Substitute.For<IMousePositionProvider>();
        _ = _mockPositionProvider.IsSupported.Returns(returnThis: true);
        _mockInputSimulatorFactory = Substitute.For<Func<IInputSimulator>>();
        _mockEnvironmentDetector = Substitute.For<ILinuxEnvironmentDetector>();

        // We use REAL selectors to verify the whole chain works as expected
        _selectors = new List<ICoordinateStrategySelector>
        {
            new ForceRelativeStrategySelector(),
            new WaylandAbsoluteStrategySelector(_mockPositionProvider),
            new WaylandRelativeStrategySelector(),
            new X11AbsoluteStrategySelector(_mockPositionProvider),
            new X11RelativeStrategySelector(),
        };

        _factory = new LinuxCoordinateStrategyFactory(_selectors, _mockEnvironmentDetector);
    }

    [LinuxFact]
    public void ForceRelative_ShouldReturnRelativeStrategy_WhenRequested()
    {
        // Arrange
        _ = _mockEnvironmentDetector.DetectedCompositor.Returns(CompositorType.KDE);
        _ = _mockEnvironmentDetector.IsWayland.Returns(returnThis: true);

        // Act
        // UseAbsolute=True, ForceRelative=True. ForceRelative should win.
        var result = _factory.Create(useAbsoluteCoordinates: true, forceRelative: true, skipInitialZero: false);

        // Assert
        _ = Assert.IsType<CrossMacro.Platform.Abstractions.Recording.Strategies.RelativeCoordinateStrategy>(result);
    }

    [LinuxFact]
    public void Wayland_Absolute_ShouldReturnEvdevAbsoluteStrategy()
    {
        // Arrange
        _ = _mockEnvironmentDetector.DetectedCompositor.Returns(CompositorType.GNOME);
        _ = _mockEnvironmentDetector.IsWayland.Returns(returnThis: true);

        // Act
        var result = _factory.Create(useAbsoluteCoordinates: true, forceRelative: false, skipInitialZero: false);

        // Assert
        _ = Assert.IsType<EvdevAbsoluteStrategy>(result);
    }

    [LinuxFact]
    public void Wayland_Absolute_WhenProviderUnsupported_ShouldFallbackToRelativeStrategy()
    {
        // Arrange
        _ = _mockEnvironmentDetector.DetectedCompositor.Returns(CompositorType.Other);
        _ = _mockEnvironmentDetector.IsWayland.Returns(returnThis: true);
        _ = _mockPositionProvider.IsSupported.Returns(returnThis: false);

        // Act
        var result = _factory.Create(useAbsoluteCoordinates: true, forceRelative: false, skipInitialZero: false);

        // Assert
        _ = Assert.IsType<CrossMacro.Platform.Abstractions.Recording.Strategies.RelativeCoordinateStrategy>(result);
    }

    [LinuxFact]
    public void Wayland_DegradedAbsolutePath_ShouldUseRelativeStrategy()
    {
        _ = _mockEnvironmentDetector.DetectedCompositor.Returns(CompositorType.GNOME);
        _ = _mockEnvironmentDetector.IsWayland.Returns(returnThis: true);
        _ = _mockPositionProvider.IsSupported.Returns(returnThis: false);

        var result = _factory.Create(useAbsoluteCoordinates: true, forceRelative: false, skipInitialZero: true);

        _ = Assert.IsType<CrossMacro.Platform.Abstractions.Recording.Strategies.RelativeCoordinateStrategy>(result);
    }

    [LinuxFact]
    public void Wayland_Relative_ShouldReturnRelativeStrategy()
    {
        // Arrange
        _ = _mockEnvironmentDetector.DetectedCompositor.Returns(CompositorType.GNOME);
        _ = _mockEnvironmentDetector.IsWayland.Returns(returnThis: true);

        // Act
        var result = _factory.Create(useAbsoluteCoordinates: false, forceRelative: false, skipInitialZero: false);

        // Assert
        _ = Assert.IsType<CrossMacro.Platform.Abstractions.Recording.Strategies.RelativeCoordinateStrategy>(result);
    }

    [LinuxFact]
    public void X11_Absolute_ShouldReturnAbsoluteStrategy()
    {
        // Arrange
        _ = _mockEnvironmentDetector.DetectedCompositor.Returns(CompositorType.X11);
        _ = _mockEnvironmentDetector.IsWayland.Returns(returnThis: false);

        // Act
        var result = _factory.Create(useAbsoluteCoordinates: true, forceRelative: false, skipInitialZero: false);

        // Assert
        _ = Assert.IsType<CrossMacro.Platform.Linux.Strategies.AbsoluteCoordinateStrategy>(result);
    }

    [LinuxFact]
    public void X11_Relative_ShouldReturnRelativeStrategy()
    {
        // Arrange
        _ = _mockEnvironmentDetector.DetectedCompositor.Returns(CompositorType.X11);
        _ = _mockEnvironmentDetector.IsWayland.Returns(returnThis: false);

        // Act
        var result = _factory.Create(useAbsoluteCoordinates: false, forceRelative: false, skipInitialZero: false);

        // Assert
        _ = Assert.IsType<CrossMacro.Platform.Abstractions.Recording.Strategies.RelativeCoordinateStrategy>(result);
    }

    [LinuxFact]
    public void Create_WhenNoSelectorMatches_ShouldReturnRelativeStrategy()
    {
        // Arrange
        _ = _mockEnvironmentDetector.DetectedCompositor.Returns(CompositorType.Unknown);
        _ = _mockEnvironmentDetector.IsWayland.Returns(returnThis: false);
        var factory = new LinuxCoordinateStrategyFactory(new List<ICoordinateStrategySelector>(), _mockEnvironmentDetector);

        // Act
        var result = factory.Create(useAbsoluteCoordinates: true, forceRelative: false, skipInitialZero: false);

        // Assert
        _ = Assert.IsType<CrossMacro.Platform.Abstractions.Recording.Strategies.RelativeCoordinateStrategy>(result);
    }

    [LinuxFact]
    public void Create_ForwardsSkipInitialZeroIntoSelectorContext()
    {
        // Arrange
        var selector = Substitute.For<ICoordinateStrategySelector>();
        _ = selector.Priority.Returns(10);
        _ = selector.CanHandle(Arg.Any<StrategyContext>()).Returns(returnThis: true);

        var expected = Substitute.For<ICoordinateStrategy>();
        _ = selector.Create(Arg.Any<StrategyContext>()).Returns(expected);

        _ = _mockEnvironmentDetector.DetectedCompositor.Returns(CompositorType.GNOME);
        _ = _mockEnvironmentDetector.IsWayland.Returns(returnThis: true);
        var factory = new LinuxCoordinateStrategyFactory([selector], _mockEnvironmentDetector);

        // Act
        var result = factory.Create(useAbsoluteCoordinates: true, forceRelative: false, skipInitialZero: true);

        // Assert
        Assert.Same(expected, result);
        _ = selector.Received(1).CanHandle(Arg.Is<StrategyContext>(c =>
            c.SkipInitialZero &&
            c.IsWayland &&
            c.Compositor == CompositorType.GNOME &&
            c.UseAbsoluteCoordinates));
    }
}
