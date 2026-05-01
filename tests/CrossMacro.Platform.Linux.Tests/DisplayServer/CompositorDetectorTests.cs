namespace CrossMacro.Platform.Linux.Tests.DisplayServer;

using CrossMacro.Platform.Linux.DisplayServer;
using CrossMacro.Platform.Linux.Services;

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

    [Theory]
    [InlineData("GNOME", CompositorType.GNOME)]
    [InlineData("ubuntu:GNOME", CompositorType.GNOME)]
    [InlineData("KDE", CompositorType.KDE)]
    [InlineData("Hyprland", CompositorType.HYPRLAND)]
    [InlineData("Wayfire", CompositorType.WAYFIRE)]
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

        Assert.Equal(CompositorType.WAYFIRE, result);
    }

    private static LinuxEnvironmentSnapshot Snapshot(
        string? sessionType = null,
        string? waylandDisplay = null,
        string? display = null,
        string? currentDesktop = null,
        string? wayfireSocket = null)
    {
        return new LinuxEnvironmentSnapshot(
            FlatpakId: null,
            AppImage: null,
            UseDaemon: null,
            SessionType: sessionType,
            WaylandDisplay: waylandDisplay,
            Display: display,
            CurrentDesktop: currentDesktop,
            GdmSession: null,
            HyprlandInstanceSignature: null,
            RuntimeDir: null,
            WayfireSocket: wayfireSocket,
            WindowButtons: null);
    }
}
