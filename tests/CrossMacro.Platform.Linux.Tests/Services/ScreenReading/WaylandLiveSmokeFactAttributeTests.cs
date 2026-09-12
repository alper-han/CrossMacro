namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading;

public sealed class WaylandLiveSmokeFactAttributeTests
{
    [Theory]
    [InlineData(true, "1", "wayland", null, true)]
    [InlineData(true, "1", null, "wayland-0", true)]
    [InlineData(true, "1", "x11", "wayland-0", false)]
    [InlineData(true, "1", null, null, false)]
    [InlineData(true, "0", "wayland", "wayland-0", false)]
    [InlineData(false, "1", "wayland", "wayland-0", false)]
    public void CursorFact_IsEnabled_ShouldRequireOptInLinuxWayland(
        bool isLinux,
        string optInValue,
        string? sessionType,
        string? waylandDisplay,
        bool expected)
    {
        Assert.Equal(expected,
            CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland.WaylandLiveCursorFactAttribute.IsEnabled(
                isLinux,
                optInValue,
                sessionType,
                waylandDisplay));
    }

    [Theory]
    [InlineData(true, "1", "wayland", null, true)]
    [InlineData(true, "1", null, "wayland-0", true)]
    [InlineData(true, "1", "x11", "wayland-0", false)]
    [InlineData(true, "1", "x11", null, false)]
    [InlineData(true, "0", "wayland", "wayland-0", false)]
    [InlineData(false, "1", "wayland", "wayland-0", false)]
    [InlineData(true, "1", null, " ", false)]
    public void IsEnabled_ShouldRequireOptInLinuxWayland(
        bool isLinux,
        string optInValue,
        string? sessionType,
        string? waylandDisplay,
        bool expected)
    {
        Assert.Equal(expected, WaylandLiveSmokeFactAttribute.IsEnabled(
            isLinux,
            optInValue,
            sessionType,
            waylandDisplay));
    }
}
