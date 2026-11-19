namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading;

public sealed class LinuxScreenReaderBackendPolicyTests
{
    [Theory]
    [InlineData(CompositorType.Hyprland)]
    [InlineData(CompositorType.Gnome)]
    [InlineData(CompositorType.Kde)]
    [InlineData(CompositorType.Cosmic)]
    [InlineData(CompositorType.Niri)]
    [InlineData(CompositorType.Sway)]
    public void FlatpakWayland_AlwaysUsesPortalRegardlessOfCompositor(CompositorType compositor)
    {
        var order = LinuxScreenReaderBackendPolicy.GetOrder(isFlatpak: true, compositor);

        Assert.Equal([LinuxScreenReaderBackend.Portal], order);
        Assert.Equal("Flatpak", LinuxScreenReaderBackendPolicy.GetPolicyName(isFlatpak: true, compositor));
    }

    [Fact]
    public void NativeKde_PrefersKwinThenNativeFallbacksThenPortal()
    {
        var order = LinuxScreenReaderBackendPolicy.GetOrder(isFlatpak: false, CompositorType.Kde);

        Assert.Equal(
            [
                LinuxScreenReaderBackend.KWinScreenShot2,
                LinuxScreenReaderBackend.ExtImageCopy,
                LinuxScreenReaderBackend.WlrScreencopy,
                LinuxScreenReaderBackend.Portal,
            ],
            order);
    }

    [Fact]
    public void NativeNonKde_UsesDeterministicWaylandOrder()
    {
        var order = LinuxScreenReaderBackendPolicy.GetOrder(isFlatpak: false, CompositorType.Niri);

        Assert.Equal(
            [
                LinuxScreenReaderBackend.GnomeExtension,
                LinuxScreenReaderBackend.ExtImageCopy,
                LinuxScreenReaderBackend.WlrScreencopy,
                LinuxScreenReaderBackend.Portal,
            ],
            order);
    }
}
