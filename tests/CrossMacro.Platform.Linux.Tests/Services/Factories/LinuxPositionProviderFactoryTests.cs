using System.Collections.Generic;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Linux.DisplayServer;
using CrossMacro.Platform.Linux.DisplayServer.Wayland;
using CrossMacro.Platform.Linux.DisplayServer.X11;
using CrossMacro.Platform.Linux.Services;
using CrossMacro.Platform.Linux.Services.Factories;
using CrossMacro.Platform.Linux.Services.Factories.Selectors;
using CrossMacro.TestInfrastructure;
using NSubstitute;
using Xunit;

namespace CrossMacro.Platform.Linux.Tests.Services.Factories;

public class LinuxPositionProviderFactoryTests
{
    private readonly ILinuxEnvironmentDetector _mockEnvironmentDetector;
    private readonly List<IPositionProviderSelector> _selectors;
    private LinuxPositionProviderFactory? _factory;

    public LinuxPositionProviderFactoryTests()
    {
        _mockEnvironmentDetector = Substitute.For<ILinuxEnvironmentDetector>();
        _selectors = new List<IPositionProviderSelector>();
    }

    private void SetupFactory()
    {
        _factory = new LinuxPositionProviderFactory(_selectors, _mockEnvironmentDetector);
    }

    [LinuxFact]
    public void Create_ShouldUseHighPrioritySelector_WhenHandlesCheckPasses()
    {
        // Arrange
        var lowPrioritySelector = Substitute.For<IPositionProviderSelector>();
        lowPrioritySelector.Priority.Returns(10);
        lowPrioritySelector.CanHandle(Arg.Any<CompositorType>()).Returns(true);
        lowPrioritySelector.Create().Returns(Substitute.For<IMousePositionProvider>());

        var highPrioritySelector = Substitute.For<IPositionProviderSelector>();
        highPrioritySelector.Priority.Returns(100);
        highPrioritySelector.CanHandle(Arg.Any<CompositorType>()).Returns(true);
        var expectedProvider = Substitute.For<IMousePositionProvider>();
        highPrioritySelector.Create().Returns(expectedProvider);

        _selectors.Add(lowPrioritySelector);
        _selectors.Add(highPrioritySelector);
        SetupFactory();

        // Act
        var result = _factory!.Create();

        // Assert
        Assert.Same(expectedProvider, result);
    }

    [LinuxFact]
    public void Create_ShouldSelectCorrectSelector_BasedOnCompositor()
    {
        // Arrange
        _mockEnvironmentDetector.DetectedCompositor.Returns(CompositorType.GNOME);

        var gnomeSelector = Substitute.For<IPositionProviderSelector>();
        gnomeSelector.CanHandle(CompositorType.GNOME).Returns(true);
        var gnomeProvider = Substitute.For<IMousePositionProvider>();
        gnomeSelector.Create().Returns(gnomeProvider);

        var kdeSelector = Substitute.For<IPositionProviderSelector>();
        kdeSelector.CanHandle(CompositorType.GNOME).Returns(false); // Can't handle Gnome

        _selectors.Add(gnomeSelector);
        _selectors.Add(kdeSelector);
        SetupFactory();

        // Act
        var result = _factory!.Create();

        // Assert
        Assert.Same(gnomeProvider, result);
    }

    [LinuxFact]
    public void Create_ShouldReturnFallback_WhenNoSelectorMatches()
    {
        // Arrange
        _mockEnvironmentDetector.DetectedCompositor.Returns(CompositorType.Unknown);
        // Empty selectors list
        SetupFactory();

        // Act
        var result = _factory!.Create();

        // Assert
        Assert.IsType<FallbackPositionProvider>(result);
    }

    [LinuxFact]
    public void Create_ShouldReturnFallback_WhenSelectorsExistButNoneCanHandle()
    {
        // Arrange
        _mockEnvironmentDetector.DetectedCompositor.Returns(CompositorType.KDE);

        var selectorA = Substitute.For<IPositionProviderSelector>();
        selectorA.CanHandle(CompositorType.KDE).Returns(false);

        var selectorB = Substitute.For<IPositionProviderSelector>();
        selectorB.CanHandle(CompositorType.KDE).Returns(false);

        _selectors.Add(selectorA);
        _selectors.Add(selectorB);
        SetupFactory();

        // Act
        var result = _factory!.Create();

        // Assert
        Assert.IsType<FallbackPositionProvider>(result);
        selectorA.DidNotReceive().Create();
        selectorB.DidNotReceive().Create();
    }
}
