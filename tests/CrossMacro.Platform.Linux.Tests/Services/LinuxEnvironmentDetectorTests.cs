namespace CrossMacro.Platform.Linux.Tests.Services;

using CrossMacro.Platform.Linux.DisplayServer;
using CrossMacro.Platform.Linux.Services;

public class LinuxEnvironmentDetectorTests
{
    [Fact]
    public void DetectedCompositor_ShouldReturnStableValueAcrossReads()
    {
        var detector = new LinuxEnvironmentDetector();

        var first = detector.DetectedCompositor;
        var second = detector.DetectedCompositor;

        Assert.Equal(first, second);
    }

    [Fact]
    public void IsX11_ShouldMatchDetectedCompositor()
    {
        var detector = new LinuxEnvironmentDetector();

        Assert.Equal(detector.DetectedCompositor == CompositorType.X11, detector.IsX11);
    }

    [Fact]
    public void IsWayland_ShouldMatchWaylandCompositorSet()
    {
        var detector = new LinuxEnvironmentDetector();
        var compositor = detector.DetectedCompositor;

        var expected = compositor == CompositorType.HYPRLAND
            || compositor == CompositorType.GNOME
            || compositor == CompositorType.KDE
            || compositor == CompositorType.Other;

        Assert.Equal(expected, detector.IsWayland);
        Assert.False(detector.IsWayland && detector.IsX11);
    }
}
