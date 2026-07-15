using CrossMacro.Platform.Linux.DisplayServer.Wayland;
using CrossMacro.Platform.Linux.Services;
using CrossMacro.Platform.Linux.Services.ScreenReading;

namespace CrossMacro.Platform.Linux.Tests.Services;

public sealed class LinuxCapabilitySnapshotProviderTests
{
    [Theory]
    [InlineData("wayland", "wayland-0", null, "Other")]
    [InlineData("x11", null, ":0", "X11")]
    public void InvalidateCache_RebuildsEnvironmentAndScreenReadingSnapshot(
        string sessionType,
        string? waylandDisplay,
        string? display,
        string expectedCompositor)
    {
        var environment = new Dictionary<string, string?>
        {
            ["XDG_SESSION_TYPE"] = sessionType,
            ["WAYLAND_DISPLAY"] = waylandDisplay,
            ["DISPLAY"] = display,
        };
        var extProbe = new MutableExtImageCopyProbe(ExtImageCopySupportResult.Unsupported("initial"));
        var screenDetector = new LinuxScreenReaderCapabilityDetector(
            extProbe,
            new FixedWlrProbe(),
            new FixedPortalProbe(),
            new FixedKWinProbe());
        var inputDetector = new LinuxInputCapabilityDetector(
            _ => false,
            _ => false,
            _ => false,
            (_, _) => LinuxInputCapabilityDetector.DaemonHandshakeProbeResult.Failed(),
            () => [],
            () => DateTime.UtcNow);
        var provider = new LinuxCapabilitySnapshotProvider(
            new LinuxEnvironmentVariables(name => environment.TryGetValue(name, out var value) ? value : null),
            inputDetector,
            screenDetector);

        Assert.Equal(expectedCompositor, provider.GetSnapshot().Compositor.ToString());
        extProbe.Result = ExtImageCopySupportResult.Supported();
        environment["XDG_SESSION_TYPE"] = "x11";
        environment["WAYLAND_DISPLAY"] = null;
        environment["DISPLAY"] = ":0";

        provider.InvalidateCache();

        var refreshed = provider.GetSnapshot();
        Assert.Equal("X11", refreshed.Compositor.ToString());
        Assert.True(refreshed.ScreenReading.ExtImageCopy.IsAvailable);
    }

    private sealed class MutableExtImageCopyProbe(ExtImageCopySupportResult result) : IExtImageCopySupportProbe
    {
        public ExtImageCopySupportResult Result { get; set; } = result;
        public ExtImageCopySupportResult ProbeSupport() => Result;
    }

    private sealed class FixedWlrProbe : IWlrScreencopySupportProbe
    {
        public WlrScreencopySupportResult ProbeSupport() => WlrScreencopySupportResult.Unsupported("wlr");
    }

    private sealed class FixedPortalProbe : IPortalScreenCastSupportProbe
    {
        public PortalScreenCastSupportResult ProbeSupport() => PortalScreenCastSupportResult.Unsupported("portal");
    }

    private sealed class FixedKWinProbe : IKWinScreenShotSupportProbe
    {
        public KWinScreenShotSupportResult ProbeSupport() => KWinScreenShotSupportResult.Unsupported("kwin");
    }
}
