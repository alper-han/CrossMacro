using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.DisplayServer.X11;
using CrossMacro.Platform.Linux.Services.ScreenReading;
using CrossMacro.Platform.Linux.Tests.Services.ScreenReading.Fakes;

namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading;

public sealed class LinuxScreenFrameProviderSelectorFixtureTests
{
    [Fact]
    public void Create_WhenFlatpakWaylandAndPortalAndExtUnavailable_FallsBackToWlr()
    {
        var fixture = ScreenReadingSelectorFixture.Wayland(
            isFlatpak: true,
            Unavailable(LinuxScreenReaderBackend.ExtImageCopy, ScreenReadErrorKind.BackendUnavailable, "ext unavailable"),
            LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.WlrScreencopy),
            Unavailable(LinuxScreenReaderBackend.Portal, ScreenReadErrorKind.PermissionDenied, "portal denied"));
        var factory = fixture.CreateFactory();

        using var provider = factory.Create();

        Assert.Equal("wlr", provider.ProviderName);
        Assert.Equal(0, fixture.PortalCreateCount);
        Assert.Equal(0, fixture.ExtCreateCount);
        Assert.Equal(1, fixture.WlrCreateCount);
    }

    [Fact]
    public void Create_WhenNativeWaylandExtAvailable_DoesNotCreateLowerPriorityBackends()
    {
        var fixture = AllAvailable(isFlatpak: false);
        var factory = fixture.CreateFactory();

        using var provider = factory.Create();

        Assert.Equal("ext", provider.ProviderName);
        Assert.Equal(1, fixture.ExtCreateCount);
        Assert.Equal(0, fixture.WlrCreateCount);
        Assert.Equal(0, fixture.PortalCreateCount);
    }

    [Fact]
    public void Create_WhenFlatpakPortalAvailable_DoesNotCreateLowerPriorityBackends()
    {
        var fixture = AllAvailable(isFlatpak: true);
        var factory = fixture.CreateFactory();

        using var provider = factory.Create();

        Assert.Equal("portal", provider.ProviderName);
        Assert.Equal(1, fixture.PortalCreateCount);
        Assert.Equal(0, fixture.ExtCreateCount);
        Assert.Equal(0, fixture.WlrCreateCount);
    }

    [Theory]
    [InlineData(false, "ExtImageCopy: ext missing, WlrScreencopy: wlr missing, Portal: portal denied")]
    [InlineData(true, "Portal: portal denied, ExtImageCopy: ext missing, WlrScreencopy: wlr missing")]
    public void Create_WhenNoBackendAvailable_ComposesAttemptedBackendsInPolicyOrder(bool isFlatpak, string expectedOrder)
    {
        var fixture = ScreenReadingSelectorFixture.Wayland(
            isFlatpak,
            Unavailable(LinuxScreenReaderBackend.ExtImageCopy, ScreenReadErrorKind.BackendUnavailable, "ext missing"),
            Unavailable(LinuxScreenReaderBackend.WlrScreencopy, ScreenReadErrorKind.BackendUnavailable, "wlr missing"),
            Unavailable(LinuxScreenReaderBackend.Portal, ScreenReadErrorKind.PermissionDenied, "portal denied"));
        var factory = fixture.CreateFactory();

        using var provider = factory.Create();

        var unavailable = Assert.IsType<UnavailableLinuxScreenFrameProvider>(provider);
        Assert.Contains(expectedOrder, unavailable.FailureMessage);
    }

    [Fact]
    public void Create_WhenFixtureSessionIsNotWayland_DoesNotRequestSnapshotOrCreateBackends()
    {
        var fixture = ScreenReadingSelectorFixture.NonWayland();
        var factory = fixture.CreateFactory();

        using var provider = factory.Create();

        Assert.False(provider.IsSupported);
        Assert.False(fixture.SnapshotRequested);
        Assert.Equal(0, fixture.ExtCreateCount);
        Assert.Equal(0, fixture.WlrCreateCount);
        Assert.Equal(0, fixture.PortalCreateCount);
    }

    [Fact]
    public void Create_WhenFixtureSessionIsX11_ProbesAndCreatesX11Only()
    {
        var fixture = ScreenReadingSelectorFixture.X11(X11ScreenCaptureSupportResult.Supported());
        var factory = fixture.CreateFactory();

        using var provider = factory.Create();

        Assert.Equal("x11", provider.ProviderName);
        Assert.False(fixture.SnapshotRequested);
        Assert.Equal(1, fixture.X11ProbeCalls);
        Assert.Equal(1, fixture.X11CreateCount);
        Assert.Equal(0, fixture.ExtCreateCount);
        Assert.Equal(0, fixture.WlrCreateCount);
        Assert.Equal(0, fixture.PortalCreateCount);
    }

    private static ScreenReadingSelectorFixture AllAvailable(bool isFlatpak) =>
        ScreenReadingSelectorFixture.Wayland(
            isFlatpak,
            LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.ExtImageCopy),
            LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.WlrScreencopy),
            LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.Portal));

    private static LinuxScreenReaderBackendCapability Unavailable(
        LinuxScreenReaderBackend backend,
        ScreenReadErrorKind errorKind,
        string message) =>
        LinuxScreenReaderBackendCapability.Unavailable(backend, errorKind, message);
}
