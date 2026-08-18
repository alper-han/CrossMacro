
namespace CrossMacro.Platform.Linux.Tests.Services;

public sealed class LinuxCapabilitySnapshotProviderTests
{
    [Fact]
    public void InvalidateScreenReadingCache_DoesNotReprobeInputDaemon()
    {
        var daemonProbeCount = 0;
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["XDG_SESSION_TYPE"] = "wayland",
            ["WAYLAND_DISPLAY"] = "wayland-0",
            ["DISPLAY"] = null,
        };
        var screenDetector = new LinuxScreenReaderCapabilityDetector(
            new FixedExtImageCopyProbe(),
            new FixedWlrProbe(),
            new FixedPortalProbe(),
            new FixedKWinProbe());
        var inputDetector = new LinuxInputCapabilityDetector(
            _ => true,
            _ => false,
            _ => false,
            (_, _) =>
            {
                daemonProbeCount++;
                return LinuxInputCapabilityDetector.DaemonHandshakeProbeResult.Failed();
            },
            () => [],
            () => DateTime.UtcNow);
        var provider = new LinuxCapabilitySnapshotProvider(
            new LinuxEnvironmentVariables(name => environment.TryGetValue(name, out var value) ? value : null),
            inputDetector,
            screenDetector);

        _ = provider.GetSnapshot();
        provider.InvalidateScreenReadingCache();
        _ = provider.GetSnapshot();

        Assert.Equal(1, daemonProbeCount);
    }

    [Fact]
    public void InvalidateCache_WhenSessionChangesToX11_SkipsWaylandScreenProbes()
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["XDG_SESSION_TYPE"] = "wayland",
            ["WAYLAND_DISPLAY"] = "wayland-0",
            ["DISPLAY"] = null,
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

        Assert.Equal(CompositorType.Other, provider.GetSnapshot().Compositor);
        Assert.Equal(1, extProbe.CallCount);
        extProbe.Result = ExtImageCopySupportResult.Supported();
        environment["XDG_SESSION_TYPE"] = "x11";
        environment["WAYLAND_DISPLAY"] = null;
        environment["DISPLAY"] = ":0";

        provider.InvalidateCache();

        var refreshed = provider.GetSnapshot();
        Assert.Equal(CompositorType.X11, refreshed.Compositor);
        Assert.False(refreshed.ScreenReading.ExtImageCopy.IsAvailable);
        Assert.Equal(ScreenReadErrorKind.Unsupported, refreshed.ScreenReading.ExtImageCopy.ErrorKind);
        Assert.Equal(1, extProbe.CallCount);
    }

    private sealed class MutableExtImageCopyProbe(ExtImageCopySupportResult result) : IExtImageCopySupportProbe
    {
        public ExtImageCopySupportResult Result { get; set; } = result;
        public int CallCount { get; private set; }

        public ExtImageCopySupportResult ProbeSupport()
        {
            CallCount++;
            return Result;
        }
    }

    private sealed class FixedExtImageCopyProbe : IExtImageCopySupportProbe
    {
        public ExtImageCopySupportResult ProbeSupport() => ExtImageCopySupportResult.Unsupported("ext-image-copy");
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
