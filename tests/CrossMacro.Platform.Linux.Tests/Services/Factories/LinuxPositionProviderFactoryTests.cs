using CrossMacro.Platform.Linux.DisplayServer.Wayland.Gnome;

namespace CrossMacro.Platform.Linux.Tests.Services.Factories;

public sealed class LinuxPositionProviderFactoryTests : IDisposable
{
    private static readonly LinuxEnvironmentSnapshot NonWaylandEnvironment = default(LinuxEnvironmentSnapshot) with
    {
        SessionType = "x11",
        Display = ":0",
    };

    private readonly GnomePositionProvider _gnome = new(NonWaylandEnvironment);
    private readonly KdePositionProvider _kde = new(NonWaylandEnvironment);

    public void Dispose()
    {
        _gnome.Dispose();
        _kde.Dispose();
    }

    [Fact]
    public void Create_WhenCompositorIsUnknown_ReturnsFallback()
    {
        var factory = CreateFactory(CreateSnapshot(CompositorType.Unknown, extImageCopyAvailable: false));

        using var provider = factory.Create();

        _ = Assert.IsType<FallbackPositionProvider>(provider);
    }

    [Fact]
    public void Create_ForKde_ReusesTheRegisteredProvider()
    {
        var factory = CreateFactory(CreateSnapshot(CompositorType.Kde, extImageCopyAvailable: true));

        var provider = factory.Create();

        Assert.Same(_kde, provider);
    }

    [Fact]
    public void Create_ForNiri_CreatesResolutionOnlyProvider()
    {
        var factory = CreateFactory(CreateSnapshot(CompositorType.Niri, extImageCopyAvailable: false));

        using var provider = factory.Create();

        _ = Assert.IsType<NiriPositionProvider>(provider);
        Assert.False(provider.IsSupported);
        Assert.Equal("Niri IPC (Resolution Only)", provider.ProviderName);
    }

    [Fact]
    public void Create_ForCosmic_CreatesResolutionOnlyProvider()
    {
        var factory = CreateFactory(CreateSnapshot(CompositorType.Cosmic, extImageCopyAvailable: false));

        using var provider = factory.Create();

        _ = Assert.IsType<CosmicPositionProvider>(provider);
        Assert.False(provider.IsSupported);
        Assert.Equal("COSMIC RandR (Resolution Only)", provider.ProviderName);
    }

    [Fact]
    public void Create_WhenNativeCursorProtocolIsAvailable_WrapsResolutionOnlyProviderIndependentlyOfScreenCaptureProbe()
    {
        var nativeCursor = CreateNativeCursor(positionAvailable: true);
        var factory = CreateFactory(
            CreateSnapshot(CompositorType.Niri, extImageCopyAvailable: false),
            () => nativeCursor);

        using var provider = factory.Create();

        var composite = Assert.IsType<CompositeMousePositionProvider>(provider);
        Assert.True(composite.SupportsAbsolutePosition);
        Assert.Contains("Niri IPC", composite.ProviderName, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_OnSway_UsesNativeCursorProviderWhenAvailable()
    {
        var nativeCursor = CreateNativeCursor(positionAvailable: true);
        var protocolFactoryCalls = 0;
        var factory = CreateFactory(
            CreateSnapshot(CompositorType.Sway, extImageCopyAvailable: true),
            () =>
            {
                protocolFactoryCalls++;
                return nativeCursor;
            });

        using var provider = factory.Create();

        var composite = Assert.IsType<CompositeMousePositionProvider>(provider);
        Assert.Contains("Wayland native cursor", composite.ProviderName, StringComparison.Ordinal);
        Assert.True(composite.SupportsAbsolutePosition);
        Assert.Equal(1, protocolFactoryCalls);
    }

    [Fact]
    public void Create_OnSway_WhenNativeCursorHasNoPositionKeepsCapabilityButUsesRelativeAvailability()
    {
        var nativeCursor = CreateNativeCursor(positionAvailable: false);
        var factory = CreateFactory(
            CreateSnapshot(CompositorType.Sway, extImageCopyAvailable: true),
            () => nativeCursor);

        using var provider = factory.Create();

        var composite = Assert.IsType<CompositeMousePositionProvider>(provider);
        Assert.Contains("Wayland native cursor", composite.ProviderName, StringComparison.Ordinal);
        Assert.True(composite.SupportsAbsolutePosition);
        Assert.False(composite.IsPositionAvailable);
        Assert.False(composite.HasUsableAbsolutePosition());
    }

    [Fact]
    public void Create_WhenWaylandCompositorHasNoSpecificProvider_StillUsesNativeCursorProtocol()
    {
        var nativeCursor = Substitute.For<IMousePositionProvider, IMousePositionChangeSource>();
        _ = nativeCursor.ProviderName.Returns("Wayland native cursor");
        _ = nativeCursor.IsSupported.Returns(returnThis: true);
        _ = nativeCursor.SupportsAbsolutePosition.Returns(returnThis: true);
        var factory = CreateFactory(
            CreateSnapshot(CompositorType.Other, extImageCopyAvailable: false),
            () => nativeCursor);

        using var provider = factory.Create();

        var composite = Assert.IsType<CompositeMousePositionProvider>(provider);
        Assert.True(composite.SupportsAbsolutePosition);
        Assert.Contains("Relative Only", composite.ProviderName, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_WhenCompositorProviderAlreadyPublishesPositions_DoesNotCreateProtocolProvider()
    {
        var protocolFactoryCalls = 0;
        var factory = CreateFactory(
            CreateSnapshot(CompositorType.Kde, extImageCopyAvailable: true),
            () =>
            {
                protocolFactoryCalls++;
                return Substitute.For<IMousePositionProvider, IMousePositionChangeSource>();
            });

        var provider = factory.Create();

        Assert.Same(_kde, provider);
        Assert.Equal(0, protocolFactoryCalls);
    }

    private LinuxPositionProviderFactory CreateFactory(
        LinuxCapabilitySnapshot snapshot,
        Func<IMousePositionProvider?>? waylandCursorProviderFactory = null)
    {
        var snapshotProvider = Substitute.For<ILinuxCapabilitySnapshotProvider>();
        _ = snapshotProvider.GetSnapshot().Returns(snapshot);
        return new LinuxPositionProviderFactory(snapshotProvider, _gnome, _kde, waylandCursorProviderFactory);
    }

    private static IMousePositionProvider CreateNativeCursor(bool positionAvailable)
    {
        var nativeCursor = Substitute.For<IMousePositionProvider, IMousePositionChangeSource, IMousePositionAvailability>();
        _ = nativeCursor.ProviderName.Returns("Wayland native cursor");
        _ = nativeCursor.IsSupported.Returns(returnThis: true);
        _ = nativeCursor.SupportsAbsolutePosition.Returns(returnThis: true);
        _ = ((IMousePositionAvailability)nativeCursor).IsPositionAvailable.Returns(positionAvailable);
        return nativeCursor;
    }

    private static LinuxCapabilitySnapshot CreateSnapshot(
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
