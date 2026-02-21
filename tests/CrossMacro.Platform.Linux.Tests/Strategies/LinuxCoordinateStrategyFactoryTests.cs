using System;
using System.Collections.Generic;
using CrossMacro.Core.Services;
using CrossMacro.Core.Services.Recording.Strategies;
using CrossMacro.Platform.Linux.DisplayServer;
using CrossMacro.Platform.Linux.Services;
using CrossMacro.Platform.Linux.Strategies;
using CrossMacro.Platform.Linux.Strategies.Selectors;
using CrossMacro.TestInfrastructure;
using NSubstitute;
using Xunit;

namespace CrossMacro.Platform.Linux.Tests.Strategies;

public class LinuxCoordinateStrategyFactoryTests
{
    private readonly IMousePositionProvider _mockPositionProvider;
    private readonly Func<IInputSimulator> _mockInputSimulatorFactory;
    private readonly ILinuxEnvironmentDetector _mockEnvironmentDetector;
    private readonly List<ICoordinateStrategySelector> _selectors;
    private readonly LinuxCoordinateStrategyFactory _factory;

    public LinuxCoordinateStrategyFactoryTests()
    {
        _mockPositionProvider = Substitute.For<IMousePositionProvider>();
        _mockInputSimulatorFactory = Substitute.For<Func<IInputSimulator>>();
        _mockEnvironmentDetector = Substitute.For<ILinuxEnvironmentDetector>();

        // We use REAL selectors to verify the whole chain works as expected
        _selectors = new List<ICoordinateStrategySelector>
        {
            new ForceRelativeStrategySelector(),
            new WaylandAbsoluteStrategySelector(_mockPositionProvider),
            new WaylandRelativeStrategySelector(),
            new X11AbsoluteStrategySelector(_mockPositionProvider),
            new X11RelativeStrategySelector()
        };

        _factory = new LinuxCoordinateStrategyFactory(_selectors, _mockEnvironmentDetector);
    }

    [LinuxFact]
    public void ForceRelative_ShouldReturnRelativeStrategy_WhenRequested()
    {
        // Arrange
        _mockEnvironmentDetector.DetectedCompositor.Returns(CompositorType.KDE);
        _mockEnvironmentDetector.IsWayland.Returns(true);

        // Act
        // UseAbsolute=True, ForceRelative=True. ForceRelative should win.
        var result = _factory.Create(useAbsoluteCoordinates: true, forceRelative: true, skipInitialZero: false);

        // Assert
        Assert.IsType<RelativeCoordinateStrategy>(result);
    }

    [LinuxFact]
    public void Wayland_Absolute_ShouldReturnEvdevAbsoluteStrategy()
    {
        // Arrange
        _mockEnvironmentDetector.DetectedCompositor.Returns(CompositorType.GNOME);
        _mockEnvironmentDetector.IsWayland.Returns(true);

        // Act
        var result = _factory.Create(useAbsoluteCoordinates: true, forceRelative: false, skipInitialZero: false);

        // Assert
        Assert.IsType<EvdevAbsoluteStrategy>(result);
    }

    [LinuxFact]
    public void Wayland_Relative_ShouldReturnRelativeStrategy()
    {
        // Arrange
        _mockEnvironmentDetector.DetectedCompositor.Returns(CompositorType.GNOME);
        _mockEnvironmentDetector.IsWayland.Returns(true);

        // Act
        var result = _factory.Create(useAbsoluteCoordinates: false, forceRelative: false, skipInitialZero: false);

        // Assert
        Assert.IsType<RelativeCoordinateStrategy>(result);
    }

    [LinuxFact]
    public void X11_Absolute_ShouldReturnAbsoluteStrategy()
    {
        // Arrange
        _mockEnvironmentDetector.DetectedCompositor.Returns(CompositorType.X11);
        _mockEnvironmentDetector.IsWayland.Returns(false);

        // Act
        var result = _factory.Create(useAbsoluteCoordinates: true, forceRelative: false, skipInitialZero: false);

        // Assert
        Assert.IsType<AbsoluteCoordinateStrategy>(result);
    }

    [LinuxFact]
    public void X11_Relative_ShouldReturnRelativeStrategy()
    {
        // Arrange
        _mockEnvironmentDetector.DetectedCompositor.Returns(CompositorType.X11);
        _mockEnvironmentDetector.IsWayland.Returns(false);

        // Act
        var result = _factory.Create(useAbsoluteCoordinates: false, forceRelative: false, skipInitialZero: false);

        // Assert
        Assert.IsType<RelativeCoordinateStrategy>(result);
    }

    [LinuxFact]
    public void Create_WhenNoSelectorMatches_ShouldThrowInvalidOperationException()
    {
        // Arrange
        _mockEnvironmentDetector.DetectedCompositor.Returns(CompositorType.Unknown);
        _mockEnvironmentDetector.IsWayland.Returns(false);
        var factory = new LinuxCoordinateStrategyFactory(new List<ICoordinateStrategySelector>(), _mockEnvironmentDetector);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            factory.Create(useAbsoluteCoordinates: true, forceRelative: false, skipInitialZero: false));

        Assert.Contains("No coordinate strategy found", ex.Message, StringComparison.Ordinal);
    }

    [LinuxFact]
    public void Create_ForwardsSkipInitialZeroIntoSelectorContext()
    {
        // Arrange
        var selector = Substitute.For<ICoordinateStrategySelector>();
        selector.Priority.Returns(10);
        selector.CanHandle(Arg.Any<StrategyContext>()).Returns(true);

        var expected = Substitute.For<ICoordinateStrategy>();
        selector.Create(Arg.Any<StrategyContext>()).Returns(expected);

        _mockEnvironmentDetector.DetectedCompositor.Returns(CompositorType.GNOME);
        _mockEnvironmentDetector.IsWayland.Returns(true);
        var factory = new LinuxCoordinateStrategyFactory(new[] { selector }, _mockEnvironmentDetector);

        // Act
        var result = factory.Create(useAbsoluteCoordinates: true, forceRelative: false, skipInitialZero: true);

        // Assert
        Assert.Same(expected, result);
        selector.Received(1).CanHandle(Arg.Is<StrategyContext>(c =>
            c.SkipInitialZero &&
            c.IsWayland &&
            c.Compositor == CompositorType.GNOME &&
            c.UseAbsoluteCoordinates));
    }
}
