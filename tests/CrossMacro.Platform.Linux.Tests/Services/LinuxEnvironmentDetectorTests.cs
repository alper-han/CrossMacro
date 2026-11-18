namespace CrossMacro.Platform.Linux.Tests.Services;

public sealed class LinuxEnvironmentDetectorTests
{
    [Fact]
    public void DetectedCompositor_IsCachedAfterFirstSnapshot()
    {
        var environment = new CountingEnvironmentVariables(CreateSnapshot(sessionType: "wayland", desktop: "GNOME"));
        var detector = new LinuxEnvironmentDetector(environment);

        var first = detector.DetectedCompositor;
        var second = detector.DetectedCompositor;

        Assert.Equal(CompositorType.Gnome, first);
        Assert.Equal(first, second);
        Assert.Equal(1, environment.CaptureCalls);
    }

    [Fact]
    public void IsX11_ShouldMatchDetectedCompositor()
    {
        var detector = new LinuxEnvironmentDetector(
            new LinuxEnvironmentVariables(CreateSnapshot(sessionType: "x11", desktop: "KDE")));

        Assert.Equal(CompositorType.X11, detector.DetectedCompositor);
        Assert.True(detector.IsX11);
        Assert.False(detector.IsWayland);
    }

    [Fact]
    public void IsWayland_ShouldMatchWaylandCompositorSet()
    {
        var detector = new LinuxEnvironmentDetector(
            new LinuxEnvironmentVariables(CreateSnapshot(sessionType: "wayland", desktop: "KDE")));

        Assert.Equal(CompositorType.Kde, detector.DetectedCompositor);
        Assert.True(detector.IsWayland);
        Assert.False(detector.IsX11);
    }

    private static LinuxEnvironmentSnapshot CreateSnapshot(string sessionType, string desktop) =>
        default(LinuxEnvironmentSnapshot) with
        {
            SessionType = sessionType,
            WaylandDisplay = sessionType.Equals("wayland", StringComparison.Ordinal) ? "wayland-0" : null,
            Display = sessionType.Equals("x11", StringComparison.Ordinal) ? ":0" : null,
            CurrentDesktop = desktop,
        };

    private sealed class CountingEnvironmentVariables(LinuxEnvironmentSnapshot snapshot) : ILinuxEnvironmentVariables
    {
        public int CaptureCalls { get; private set; }

        public LinuxEnvironmentSnapshot CaptureSnapshot()
        {
            CaptureCalls++;
            return snapshot;
        }
    }
}
