namespace CrossMacro.Platform.Linux.Tests.DisplayServer;


public sealed class CompositorDetectorTests
{
    [Fact]
    public void ClassifyFromEnvironment_WhenNotLinux_ReturnsUnknown()
    {
        var result = CompositorDetector.ClassifyFromEnvironment(
            Snapshot(sessionType: "wayland", currentDesktop: "GNOME"),
            isLinux: false);

        Assert.Equal(CompositorType.Unknown, result);
    }

    [Theory]
    [InlineData("x11", null)]
    [InlineData(null, ":0")]
    public void ClassifyFromEnvironment_WhenX11Only_ReturnsX11(string? sessionType, string? display)
    {
        var result = CompositorDetector.ClassifyFromEnvironment(
            Snapshot(sessionType: sessionType, display: display));

        Assert.Equal(CompositorType.X11, result);
    }

    [Fact]
    public void ClassifyFromEnvironment_WhenMissingDisplaySignals_ReturnsUnknown()
    {
        var result = CompositorDetector.ClassifyFromEnvironment(Snapshot());

        Assert.Equal(CompositorType.Unknown, result);
    }

    [Fact]
    public void ClassifyFromEnvironment_WhenWaylandAndDisplayConflict_DoesNotForceX11()
    {
        var result = CompositorDetector.ClassifyFromEnvironment(
            Snapshot(sessionType: "wayland", waylandDisplay: "wayland-0", display: ":0"));

        Assert.Equal(CompositorType.Other, result);
    }

    [Theory]
    [InlineData("wayland", null)]
    [InlineData(null, "wayland-0")]
    public void ClassifyFromEnvironment_WhenGenericWayland_ReturnsOther(string? sessionType, string? waylandDisplay)
    {
        var result = CompositorDetector.ClassifyFromEnvironment(
            Snapshot(sessionType: sessionType, waylandDisplay: waylandDisplay));

        Assert.Equal(CompositorType.Other, result);
    }

    [Fact]
    public void ClassifyFromEnvironment_WhenWaylandDesktopIsUnknown_ReturnsOther()
    {
        var result = CompositorDetector.ClassifyFromEnvironment(
            Snapshot(sessionType: "wayland", waylandDisplay: "wayland-0", currentDesktop: "CustomDesktop"));

        Assert.Equal(CompositorType.Other, result);
    }

    [Theory]
    [InlineData("GNOME", CompositorType.Gnome)]
    [InlineData("ubuntu:GNOME", CompositorType.Gnome)]
    [InlineData("KDE", CompositorType.Kde)]
    [InlineData("Hyprland", CompositorType.Hyprland)]
    [InlineData("Wayfire", CompositorType.Wayfire)]
    [InlineData("niri", CompositorType.Niri)]
    [InlineData("niri:GNOME", CompositorType.Niri)]
    [InlineData("COSMIC", CompositorType.Cosmic)]
    [InlineData("pop:COSMIC", CompositorType.Cosmic)]
    [InlineData("Sway", CompositorType.Sway)]
    [InlineData("sway", CompositorType.Sway)]
    public void ClassifyFromEnvironment_WhenKnownWaylandDesktop_ReturnsCompositor(
        string currentDesktop,
        CompositorType expected)
    {
        var result = CompositorDetector.ClassifyFromEnvironment(
            Snapshot(sessionType: "wayland", currentDesktop: currentDesktop));

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ClassifyFromEnvironment_WhenWayfireSocketIsSet_ReturnsWayfire()
    {
        var result = CompositorDetector.ClassifyFromEnvironment(
            Snapshot(sessionType: "wayland", wayfireSocket: "/run/user/1000/wayfire.sock"));

        Assert.Equal(CompositorType.Wayfire, result);
    }

    [Fact]
    public void ClassifyFromEnvironment_WhenSwaySocketIsSet_ReturnsSway()
    {
        var result = CompositorDetector.ClassifyFromEnvironment(
            Snapshot(sessionType: "wayland", swaySocket: "/run/user/1000/sway-ipc.1234.sock"));

        Assert.Equal(CompositorType.Sway, result);
    }

    [Fact]
    public void ClassifyFromEnvironment_WhenGdmSessionIsNiri_ReturnsNiri()
    {
        var result = CompositorDetector.ClassifyFromEnvironment(
            Snapshot(sessionType: "wayland", gdmSession: "niri"));

        Assert.Equal(CompositorType.Niri, result);
    }

    [Fact]
    public void ClassifyFromEnvironment_WhenGdmSessionIsCosmic_ReturnsCosmic()
    {
        var result = CompositorDetector.ClassifyFromEnvironment(
            Snapshot(sessionType: "wayland", gdmSession: "cosmic"));

        Assert.Equal(CompositorType.Cosmic, result);
    }

    [Fact]
    public void ClassifyFromEnvironment_WhenX11SessionHasNiriHint_ReturnsX11()
    {
        var result = CompositorDetector.ClassifyFromEnvironment(
            Snapshot(sessionType: "x11", display: ":0", currentDesktop: "niri", gdmSession: "niri"));

        Assert.Equal(CompositorType.X11, result);
    }

    [Fact]
    public void ClassifyFromEnvironment_WhenX11SessionHasCosmicHint_ReturnsX11()
    {
        var result = CompositorDetector.ClassifyFromEnvironment(
            Snapshot(sessionType: "x11", display: ":0", currentDesktop: "COSMIC", gdmSession: "cosmic"));

        Assert.Equal(CompositorType.X11, result);
    }

    private static LinuxEnvironmentSnapshot Snapshot(
        string? sessionType = null,
        string? waylandDisplay = null,
        string? display = null,
        string? currentDesktop = null,
        string? gdmSession = null,
        string? wayfireSocket = null,
        string? swaySocket = null)
    {
        return default(LinuxEnvironmentSnapshot) with
        {
            SessionType = sessionType,
            WaylandDisplay = waylandDisplay,
            Display = display,
            CurrentDesktop = currentDesktop,
            GdmSession = gdmSession,
            WayfireSocket = wayfireSocket,
            SwaySocket = swaySocket,
        };
    }
}
