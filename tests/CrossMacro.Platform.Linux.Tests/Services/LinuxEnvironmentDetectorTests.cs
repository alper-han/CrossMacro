namespace CrossMacro.Platform.Linux.Tests.Services;

using CrossMacro.Platform.Linux.DisplayServer;
using CrossMacro.Platform.Linux.Services;

public class LinuxEnvironmentDetectorTests
{
    [Fact]
    public void DetectedCompositor_ShouldReturnStableValueAcrossReads()
    {
        var detector = new LinuxEnvironmentDetector(
            new LinuxEnvironmentVariables(LinuxEnvironmentVariables.CaptureCurrentSnapshot()));

        var first = detector.DetectedCompositor;
        var second = detector.DetectedCompositor;

        Assert.Equal(first, second);
    }

    [Fact]
    public void IsX11_ShouldMatchDetectedCompositor()
    {
        var detector = new LinuxEnvironmentDetector(
            new LinuxEnvironmentVariables(LinuxEnvironmentVariables.CaptureCurrentSnapshot()));

        Assert.Equal(detector.DetectedCompositor is CompositorType.X11, detector.IsX11);
    }

    [Fact]
    public void IsWayland_ShouldMatchWaylandCompositorSet()
    {
        var detector = new LinuxEnvironmentDetector(
            new LinuxEnvironmentVariables(LinuxEnvironmentVariables.CaptureCurrentSnapshot()));
        var compositor = detector.DetectedCompositor;

        var expected = compositor is CompositorType.HYPRLAND
or CompositorType.WAYFIRE
or CompositorType.NIRI
or CompositorType.COSMIC
or CompositorType.GNOME
or CompositorType.KDE
or CompositorType.Other;

        Assert.Equal(expected, detector.IsWayland);
        Assert.False(detector.IsWayland && detector.IsX11);
    }
}
