
namespace CrossMacro.Platform.Linux.Tests.Services.Factories;

public sealed class LinuxPositionProviderFactoryTests
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
        _ = lowPrioritySelector.Priority.Returns(10);
        _ = lowPrioritySelector.CanHandle(Arg.Any<CompositorType>()).Returns(returnThis: true);
        _ = lowPrioritySelector.Create().Returns(Substitute.For<IMousePositionProvider>());

        var highPrioritySelector = Substitute.For<IPositionProviderSelector>();
        _ = highPrioritySelector.Priority.Returns(100);
        _ = highPrioritySelector.CanHandle(Arg.Any<CompositorType>()).Returns(returnThis: true);
        var expectedProvider = Substitute.For<IMousePositionProvider>();
        _ = highPrioritySelector.Create().Returns(expectedProvider);

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
        _ = _mockEnvironmentDetector.DetectedCompositor.Returns(CompositorType.GNOME);

        var gnomeSelector = Substitute.For<IPositionProviderSelector>();
        _ = gnomeSelector.CanHandle(CompositorType.GNOME).Returns(returnThis: true);
        var gnomeProvider = Substitute.For<IMousePositionProvider>();
        _ = gnomeSelector.Create().Returns(gnomeProvider);

        var kdeSelector = Substitute.For<IPositionProviderSelector>();
        _ = kdeSelector.CanHandle(CompositorType.GNOME).Returns(returnThis: false); // Can't handle Gnome

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
        _ = _mockEnvironmentDetector.DetectedCompositor.Returns(CompositorType.Unknown);
        // Empty selectors list
        SetupFactory();

        // Act
        var result = _factory!.Create();

        // Assert
        _ = Assert.IsType<FallbackPositionProvider>(result);
    }

    [LinuxFact]
    public void Create_ShouldReturnFallback_WhenSelectorsExistButNoneCanHandle()
    {
        // Arrange
        _ = _mockEnvironmentDetector.DetectedCompositor.Returns(CompositorType.KDE);

        var selectorA = Substitute.For<IPositionProviderSelector>();
        _ = selectorA.CanHandle(CompositorType.KDE).Returns(returnThis: false);

        var selectorB = Substitute.For<IPositionProviderSelector>();
        _ = selectorB.CanHandle(CompositorType.KDE).Returns(returnThis: false);

        _selectors.Add(selectorA);
        _selectors.Add(selectorB);
        SetupFactory();

        // Act
        var result = _factory!.Create();

        // Assert
        _ = Assert.IsType<FallbackPositionProvider>(result);
        _ = selectorA.DidNotReceive().Create();
        _ = selectorB.DidNotReceive().Create();
    }

    [LinuxFact]
    public void NiriPositionProviderSelector_ShouldCreateResolutionOnlyProvider_ForNiri()
    {
        var selector = new NiriPositionProviderSelector();

        Assert.True(selector.CanHandle(CompositorType.NIRI));
        Assert.False(selector.CanHandle(CompositorType.Other));

        using var provider = selector.Create();
        _ = Assert.IsType<NiriPositionProvider>(provider);
        Assert.False(provider.IsSupported);
        Assert.Equal("Niri IPC (Resolution Only)", provider.ProviderName);
    }

    [LinuxFact]
    public void CosmicPositionProviderSelector_ShouldCreateResolutionOnlyProvider_ForCosmic()
    {
        var selector = new CosmicPositionProviderSelector();

        Assert.True(selector.CanHandle(CompositorType.COSMIC));
        Assert.False(selector.CanHandle(CompositorType.Other));

        using var provider = selector.Create();
        _ = Assert.IsType<CosmicPositionProvider>(provider);
        Assert.False(provider.IsSupported);
        Assert.Equal("COSMIC RandR (Resolution Only)", provider.ProviderName);
    }
}
