namespace CrossMacro.Platform.Linux.Tests.Services;

public sealed class LinuxDisplaySessionClassifierTests
{
    [Theory]
    [InlineData("wayland", null, true)]
    [InlineData("wayland", "wayland-0", true)]
    [InlineData("x11", null, false)]
    [InlineData("x11", "wayland-0", false)]
    [InlineData(null, "wayland-0", true)]
    [InlineData(null, null, false)]
    public void IsWayland_ClassifiesExplicitSessionBeforeDisplayFallback(
        string? sessionType,
        string? waylandDisplay,
        bool expected)
    {
        var environment = new LinuxEnvironmentSnapshot(
            FlatpakId: null,
            AppImage: null,
            SessionType: sessionType,
            WaylandDisplay: waylandDisplay,
            Display: ":0",
            CurrentDesktop: "KDE",
            GdmSession: null,
            HyprlandInstanceSignature: null,
            RuntimeDir: null,
            WayfireSocket: null,
            SwaySocket: null,
            WindowButtons: null);

        Assert.Equal(expected, LinuxDisplaySessionClassifier.IsWayland(environment));
    }
}
