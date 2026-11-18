
namespace CrossMacro.Platform.Linux.Tests.Strategies;

public sealed class LinuxCoordinateStrategyFactoryTests
{
    private readonly IMousePositionProvider _mockPositionProvider;
    private readonly ILinuxEnvironmentDetector _mockEnvironmentDetector;
    private readonly LinuxCoordinateStrategyFactory _factory;

    public LinuxCoordinateStrategyFactoryTests()
    {
        _mockPositionProvider = Substitute.For<IMousePositionProvider>();
        _ = _mockPositionProvider.IsSupported.Returns(returnThis: true);
        _ = _mockPositionProvider.SupportsAbsolutePosition.Returns(returnThis: true);
        _mockEnvironmentDetector = Substitute.For<ILinuxEnvironmentDetector>();
        _factory = new LinuxCoordinateStrategyFactory(_mockEnvironmentDetector, _mockPositionProvider);
    }

    [Fact]
    public void Constructor_WhenEnvironmentDetectorIsNull_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new LinuxCoordinateStrategyFactory(null!, _mockPositionProvider));

        Assert.Equal("environmentDetector", exception.ParamName);
    }

    [Fact]
    public void Constructor_WhenPositionProviderIsNull_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new LinuxCoordinateStrategyFactory(_mockEnvironmentDetector, null!));

        Assert.Equal("positionProvider", exception.ParamName);
    }

    [LinuxFact]
    public void ForceRelative_OnWaylandWithPositionProvider_ShouldReturnRawRelativeStrategy()
    {
        // Arrange
        _ = _mockEnvironmentDetector.DetectedCompositor.Returns(CompositorType.KDE);
        _ = _mockEnvironmentDetector.IsWayland.Returns(returnThis: true);

        // Act
        // UseAbsolute=True, ForceRelative=True. ForceRelative should win.
        var result = _factory.Create(useAbsoluteCoordinates: true, forceRelative: true, skipInitialZero: false);

        // Assert
        var strategy = Assert.IsType<RelativeCoordinateStrategy>(result);
        Assert.True(strategy.ProducesRelativeCoordinates);
        Assert.False(strategy.ProducesLogicalCoordinates);
    }

    [LinuxFact]
    public void ForceRelative_WithLogicalRelativeRequested_ShouldReturnLogicalRelativeStrategy()
    {
        _ = _mockEnvironmentDetector.DetectedCompositor.Returns(CompositorType.X11);
        _ = _mockEnvironmentDetector.IsWayland.Returns(returnThis: false);

        var result = _factory.Create(
            useAbsoluteCoordinates: true,
            forceRelative: true,
            skipInitialZero: false,
            useLogicalRelativeCoordinates: true);

        var strategy = Assert.IsType<X11LogicalRelativeCoordinateStrategy>(result);
        Assert.True(strategy.ProducesRelativeCoordinates);
        Assert.True(strategy.ProducesLogicalCoordinates);
    }

    [LinuxFact]
    public void ForceRelative_WithLogicalRelativeRequestedOnUnknownBackend_ThrowsClearError()
    {
        _ = _mockEnvironmentDetector.DetectedCompositor.Returns(CompositorType.Unknown);
        _ = _mockEnvironmentDetector.IsWayland.Returns(returnThis: false);

        var action = () => _factory.Create(
            useAbsoluteCoordinates: true,
            forceRelative: true,
            skipInitialZero: false,
            useLogicalRelativeCoordinates: true);

        _ = action.Should().Throw<InvalidOperationException>()
            .WithMessage("*not supported by the active input backend*");
    }

    [LinuxFact]
    public void Wayland_Absolute_ShouldReturnCompositorCoordinateStrategy()
    {
        // Arrange
        _ = _mockEnvironmentDetector.DetectedCompositor.Returns(CompositorType.GNOME);
        _ = _mockEnvironmentDetector.IsWayland.Returns(returnThis: true);

        // Act
        var result = _factory.Create(useAbsoluteCoordinates: true, forceRelative: false, skipInitialZero: false);

        // Assert
        var strategy = Assert.IsType<CompositorCoordinateStrategy>(result);
        Assert.False(strategy.ProducesRelativeCoordinates);
        Assert.True(strategy.ProducesLogicalCoordinates);
    }

    [LinuxFact]
    public void Wayland_Absolute_WhenProviderUnsupported_ShouldFallbackToRelativeStrategy()
    {
        // Arrange
        _ = _mockEnvironmentDetector.DetectedCompositor.Returns(CompositorType.Other);
        _ = _mockEnvironmentDetector.IsWayland.Returns(returnThis: true);
        _ = _mockPositionProvider.SupportsAbsolutePosition.Returns(returnThis: false);

        // Act
        var result = _factory.Create(useAbsoluteCoordinates: true, forceRelative: false, skipInitialZero: false);

        // Assert
        _ = Assert.IsType<CrossMacro.Platform.Abstractions.Recording.Strategies.RelativeCoordinateStrategy>(result);
    }

    [LinuxFact]
    public void Sway_Absolute_WhenCursorPositionIsUnavailable_ShouldRemainRelativeOnly()
    {
        _ = _mockEnvironmentDetector.DetectedCompositor.Returns(CompositorType.SWAY);
        _ = _mockEnvironmentDetector.IsWayland.Returns(returnThis: true);
        _ = _mockPositionProvider.SupportsAbsolutePosition.Returns(returnThis: false);

        var result = _factory.Create(
            useAbsoluteCoordinates: true,
            forceRelative: false,
            skipInitialZero: false);

        var strategy = Assert.IsType<RelativeCoordinateStrategy>(result);
        Assert.True(strategy.ProducesRelativeCoordinates);
        Assert.False(strategy.ProducesLogicalCoordinates);
    }

    [LinuxFact]
    public void Wayland_DegradedAbsolutePath_ShouldUseRelativeStrategy()
    {
        _ = _mockEnvironmentDetector.DetectedCompositor.Returns(CompositorType.GNOME);
        _ = _mockEnvironmentDetector.IsWayland.Returns(returnThis: true);
        _ = _mockPositionProvider.SupportsAbsolutePosition.Returns(returnThis: false);

        var result = _factory.Create(useAbsoluteCoordinates: true, forceRelative: false, skipInitialZero: true);

        _ = Assert.IsType<CrossMacro.Platform.Abstractions.Recording.Strategies.RelativeCoordinateStrategy>(result);
    }

    [LinuxFact]
    public void Wayland_Relative_WithPositionProvider_ShouldReturnLogicalCompositorStrategy()
    {
        // Arrange
        _ = _mockEnvironmentDetector.DetectedCompositor.Returns(CompositorType.GNOME);
        _ = _mockEnvironmentDetector.IsWayland.Returns(returnThis: true);

        // Act
        var result = _factory.Create(useAbsoluteCoordinates: false, forceRelative: false, skipInitialZero: false);

        // Assert
        var strategy = Assert.IsType<CompositorCoordinateStrategy>(result);
        Assert.True(strategy.ProducesRelativeCoordinates);
        Assert.True(strategy.ProducesLogicalCoordinates);
    }

    [LinuxFact]
    public void Sway_WhenNativeCursorPositionIsUnavailable_ShouldUseRawRelativeStrategy()
    {
        _ = _mockEnvironmentDetector.DetectedCompositor.Returns(CompositorType.SWAY);
        _ = _mockEnvironmentDetector.IsWayland.Returns(returnThis: true);
        _ = _mockPositionProvider.SupportsAbsolutePosition.Returns(returnThis: false);

        var result = _factory.Create(
            useAbsoluteCoordinates: false,
            forceRelative: false,
            skipInitialZero: false);

        var strategy = Assert.IsType<RelativeCoordinateStrategy>(result);
        Assert.True(strategy.ProducesRelativeCoordinates);
        Assert.False(strategy.ProducesLogicalCoordinates);
    }

    [LinuxFact]
    public void ForceRelative_OnWaylandWithoutPositionProvider_ShouldReturnRawRelativeStrategy()
    {
        _ = _mockEnvironmentDetector.DetectedCompositor.Returns(CompositorType.NIRI);
        _ = _mockEnvironmentDetector.IsWayland.Returns(returnThis: true);
        _ = _mockPositionProvider.SupportsAbsolutePosition.Returns(returnThis: false);

        var result = _factory.Create(useAbsoluteCoordinates: true, forceRelative: true, skipInitialZero: false);

        var strategy = Assert.IsType<RelativeCoordinateStrategy>(result);
        Assert.True(strategy.ProducesRelativeCoordinates);
        Assert.False(strategy.ProducesLogicalCoordinates);
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
        var strategy = Assert.IsType<X11LogicalRelativeCoordinateStrategy>(result);
        Assert.True(strategy.ProducesLogicalCoordinates);
    }

    [LinuxFact]
    public void ForceRelative_OnX11_ShouldReturnRawRelativeStrategy()
    {
        _ = _mockEnvironmentDetector.DetectedCompositor.Returns(CompositorType.X11);
        _ = _mockEnvironmentDetector.IsWayland.Returns(returnThis: false);

        var result = _factory.Create(
            useAbsoluteCoordinates: true,
            forceRelative: true,
            skipInitialZero: false);

        var strategy = Assert.IsType<RelativeCoordinateStrategy>(result);
        Assert.True(strategy.ProducesRelativeCoordinates);
        Assert.False(strategy.ProducesLogicalCoordinates);
    }

    [LinuxFact]
    public void ForceRelative_WithLogicalRelativeRequestedWithoutCursorPosition_ThrowsClearError()
    {
        _ = _mockEnvironmentDetector.DetectedCompositor.Returns(CompositorType.X11);
        _ = _mockEnvironmentDetector.IsWayland.Returns(returnThis: false);
        _ = _mockPositionProvider.SupportsAbsolutePosition.Returns(returnThis: false);

        var action = () => _factory.Create(
            useAbsoluteCoordinates: true,
            forceRelative: true,
            skipInitialZero: false,
            useLogicalRelativeCoordinates: true);

        _ = action.Should().Throw<InvalidOperationException>()
            .WithMessage("*requires access to the global cursor position*");
    }

    [LinuxFact]
    public void X11_Relative_WithoutCursorPosition_ShouldReturnRawRelativeStrategy()
    {
        _ = _mockEnvironmentDetector.DetectedCompositor.Returns(CompositorType.X11);
        _ = _mockEnvironmentDetector.IsWayland.Returns(returnThis: false);
        _ = _mockPositionProvider.SupportsAbsolutePosition.Returns(returnThis: false);

        var result = _factory.Create(useAbsoluteCoordinates: false, forceRelative: false, skipInitialZero: false);

        _ = Assert.IsType<RelativeCoordinateStrategy>(result);
    }

    [LinuxFact]
    public void ForceRelative_WithLogicalRelativeRequestedOnWayland_ShouldReturnLogicalCompositorStrategyWithoutRawFallback()
    {
        _ = _mockEnvironmentDetector.DetectedCompositor.Returns(CompositorType.GNOME);
        _ = _mockEnvironmentDetector.IsWayland.Returns(returnThis: true);

        var result = _factory.Create(
            useAbsoluteCoordinates: false,
            forceRelative: true,
            skipInitialZero: false,
            useLogicalRelativeCoordinates: true);

        var strategy = Assert.IsType<CompositorCoordinateStrategy>(result);
        Assert.True(strategy.ProducesRelativeCoordinates);
        Assert.True(strategy.ProducesLogicalCoordinates);
    }
}
