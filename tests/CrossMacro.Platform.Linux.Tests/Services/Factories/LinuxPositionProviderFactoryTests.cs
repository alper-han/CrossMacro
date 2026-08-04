
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
    public void Create_WhenNativeCursorProtocolIsAvailable_WrapsResolutionOnlyProviderIndependentlyOfScreenCaptureProbe()
    {
        var snapshotProvider = Substitute.For<ILinuxCapabilitySnapshotProvider>();
        _ = snapshotProvider.GetSnapshot().Returns(CreateWaylandSnapshot(
            CompositorType.NIRI,
            extImageCopyAvailable: false));

        var fallback = Substitute.For<IMousePositionProvider>();
        _ = fallback.ProviderName.Returns("Niri IPC (Resolution Only)");
        _ = fallback.SupportsAbsolutePosition.Returns(returnThis: false);
        var selector = Substitute.For<IPositionProviderSelector>();
        _ = selector.Priority.Returns(10);
        _ = selector.CanHandle(CompositorType.NIRI).Returns(returnThis: true);
        _ = selector.Create().Returns(fallback);

        var nativeCursor = Substitute.For<IMousePositionProvider, IMousePositionChangeSource>();
        _ = nativeCursor.ProviderName.Returns("Wayland native cursor");
        _ = nativeCursor.IsSupported.Returns(returnThis: true);
        _ = nativeCursor.SupportsAbsolutePosition.Returns(returnThis: true);
        var factory = new LinuxPositionProviderFactory(
            [selector],
            snapshotProvider,
            () => nativeCursor);

        using var provider = factory.Create();

        var composite = Assert.IsType<CompositeMousePositionProvider>(provider);
        Assert.True(composite.SupportsAbsolutePosition);
        Assert.Contains("Niri IPC", composite.ProviderName, StringComparison.Ordinal);
    }

    [LinuxFact]
    public void Create_WhenWaylandCompositorHasNoSpecificSelector_StillUsesNativeCursorProtocol()
    {
        var snapshotProvider = Substitute.For<ILinuxCapabilitySnapshotProvider>();
        _ = snapshotProvider.GetSnapshot().Returns(CreateWaylandSnapshot(
            CompositorType.Other,
            extImageCopyAvailable: false));

        var nativeCursor = Substitute.For<IMousePositionProvider, IMousePositionChangeSource>();
        _ = nativeCursor.ProviderName.Returns("Wayland native cursor");
        _ = nativeCursor.IsSupported.Returns(returnThis: true);
        _ = nativeCursor.SupportsAbsolutePosition.Returns(returnThis: true);
        var factory = new LinuxPositionProviderFactory(
            [],
            snapshotProvider,
            () => nativeCursor);

        using var provider = factory.Create();

        var composite = Assert.IsType<CompositeMousePositionProvider>(provider);
        Assert.True(composite.SupportsAbsolutePosition);
        Assert.Contains("Relative Only", composite.ProviderName, StringComparison.Ordinal);
    }

    [LinuxFact]
    public void Create_WhenCompositorProviderAlreadyPublishesPositions_DoesNotCreateProtocolProvider()
    {
        var snapshotProvider = Substitute.For<ILinuxCapabilitySnapshotProvider>();
        _ = snapshotProvider.GetSnapshot().Returns(CreateWaylandSnapshot(
            CompositorType.KDE,
            extImageCopyAvailable: true));

        var nativeProvider = Substitute.For<IMousePositionProvider, IMousePositionChangeSource>();
        var selector = Substitute.For<IPositionProviderSelector>();
        _ = selector.Priority.Returns(10);
        _ = selector.CanHandle(CompositorType.KDE).Returns(returnThis: true);
        _ = selector.Create().Returns(nativeProvider);
        var protocolFactoryCalls = 0;
        var factory = new LinuxPositionProviderFactory(
            [selector],
            snapshotProvider,
            () =>
            {
                protocolFactoryCalls++;
                return Substitute.For<IMousePositionProvider, IMousePositionChangeSource>();
            });

        var provider = factory.Create();

        Assert.Same(nativeProvider, provider);
        Assert.Equal(0, protocolFactoryCalls);
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

    private static LinuxCapabilitySnapshot CreateWaylandSnapshot(
        CompositorType compositor,
        bool extImageCopyAvailable)
    {
        var extImageCopy = extImageCopyAvailable
            ? LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.ExtImageCopy)
            : Unavailable(LinuxScreenReaderBackend.ExtImageCopy);
        var screenReading = new LinuxScreenReaderCapabilitySnapshot(
            Unavailable(LinuxScreenReaderBackend.KWinScreenShot2),
            extImageCopy,
            Unavailable(LinuxScreenReaderBackend.WlrScreencopy),
            Unavailable(LinuxScreenReaderBackend.Portal),
            Unavailable(LinuxScreenReaderBackend.GnomeExtension));
        return new LinuxCapabilitySnapshot(default, compositor, default, screenReading);
    }

    private static LinuxScreenReaderBackendCapability Unavailable(LinuxScreenReaderBackend backend) =>
        LinuxScreenReaderBackendCapability.Unavailable(
            backend,
            ScreenReadErrorKind.BackendUnavailable,
            "Unavailable in test.");
}
