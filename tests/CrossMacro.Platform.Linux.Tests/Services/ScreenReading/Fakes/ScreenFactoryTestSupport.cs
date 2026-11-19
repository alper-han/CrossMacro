
namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading.Fakes;

/// <summary>Builds a screen frame provider factory over a deterministic aggregate capability snapshot.</summary>
internal static class ScreenFactoryTestSupport
{
    private static readonly Func<GnomeExtensionSupportResult, IScreenFrameProvider> NoGnomeBackend =
        static _ => new UnavailableLinuxScreenFrameProvider(ScreenReadErrorKind.BackendUnavailable, "These tests do not use the GNOME extension backend.");

    internal static LinuxScreenFrameProviderFactory CreateFactory(
        CompositorType compositor,
        bool isFlatpak,
        ILinuxScreenReaderCapabilityDetector capabilityDetector,
        Func<WaylandSupportResult, IScreenFrameProvider> ext,
        Func<WaylandSupportResult, IScreenFrameProvider> wlr,
        Func<WaylandSupportResult, IScreenFrameProvider> portal,
        Func<WaylandSupportResult, IScreenFrameProvider> kwin,
        IX11ScreenCaptureSupportProbe x11SupportProbe,
        Func<X11ScreenCaptureSupportResult, IScreenFrameProvider> x11Factory,
        Func<GnomeExtensionSupportResult, IScreenFrameProvider>? gnome = null) =>
        new(
            Snapshots(compositor, isFlatpak, capabilityDetector),
            capabilityDetector,
            LinuxScreenBackendDescriptors.FromFactories(ext, wlr, portal, kwin, gnome ?? NoGnomeBackend),
            x11SupportProbe,
            x11Factory);

    /// <summary>Reports screen capabilities only for Wayland sessions, like the production aggregate provider.</summary>
    internal static ILinuxCapabilitySnapshotProvider Snapshots(
        CompositorType compositor,
        bool isFlatpak,
        ILinuxScreenReaderCapabilityDetector capabilityDetector)
    {
        var environment = InputSnapshotTestFactory.Environment(compositor is CompositorType.X11 ? "x11" : "wayland") with
        {
            FlatpakId = isFlatpak ? "io.github.alper_han.crossmacro" : null,
        };
        var isWayland = compositor is not CompositorType.X11 and not CompositorType.Unknown;
        var provider = Substitute.For<ILinuxCapabilitySnapshotProvider>();
        provider.GetSnapshot().Returns(_ => new LinuxCapabilitySnapshot(
            environment,
            compositor,
            default,
            isWayland ? capabilityDetector.GetSnapshot() : default));
        return provider;
    }
}
