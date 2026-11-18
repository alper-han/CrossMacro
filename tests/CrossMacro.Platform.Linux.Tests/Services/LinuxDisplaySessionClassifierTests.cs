namespace CrossMacro.Platform.Linux.Tests.Services;

public sealed class LinuxDisplaySessionClassifierTests
{
    [Theory]
    [InlineData("wayland", null, null, "Wayland")]
    [InlineData("wayland", "wayland-0", ":0", "Wayland")]
    [InlineData("WAYLAND", null, null, "Wayland")]
    [InlineData("x11", null, null, "X11")]
    [InlineData("x11", "wayland-0", ":0", "X11")]
    [InlineData(null, "wayland-0", ":0", "Wayland")]
    [InlineData(null, null, ":0", "X11")]
    [InlineData("tty", null, ":0", "X11")]
    [InlineData("tty", null, null, "Unknown")]
    [InlineData(null, null, null, "Unknown")]
    [InlineData(null, " ", " ", "Unknown")]
    public void Classify_PrefersExplicitSessionTypeThenWaylandThenX11(
        string? sessionType,
        string? waylandDisplay,
        string? display,
        string expected)
    {
        var environment = Environment(sessionType: sessionType, waylandDisplay: waylandDisplay, display: display);

        Assert.Equal(expected, LinuxDisplaySessionClassifier.Classify(environment).ToString());
        Assert.Equal(expected is "Wayland", LinuxDisplaySessionClassifier.IsWayland(environment));
    }

    [Theory]
    [InlineData("KDE", true)]
    [InlineData("KDE:Plasma", true)]
    [InlineData("plasma", true)]
    [InlineData("ubuntu:KDE", true)]
    [InlineData("GNOME", false)]
    [InlineData("kde-wayland-fork", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void IsKde_MatchesWholeColonSeparatedComponents(string? currentDesktop, bool expected)
    {
        Assert.Equal(expected, LinuxDisplaySessionClassifier.IsKde(Environment(currentDesktop: currentDesktop)));
    }

    [Theory]
    [InlineData("GNOME", true)]
    [InlineData("ubuntu:GNOME", true)]
    [InlineData("gnome-classic:GNOME", true)]
    [InlineData("KDE", false)]
    [InlineData(null, false)]
    public void IsGnome_MatchesWholeColonSeparatedComponents(string? currentDesktop, bool expected)
    {
        Assert.Equal(expected, LinuxDisplaySessionClassifier.IsGnome(Environment(currentDesktop: currentDesktop)));
    }

    [Theory]
    [InlineData("KDE:Plasma")]
    [InlineData("plasma:KDE")]
    public void CompositorDetector_WhenCurrentDesktopListsKdeAmongOthers_ReturnsKde(string currentDesktop)
    {
        var result = CompositorDetector.ClassifyFromEnvironment(
            Environment(sessionType: "wayland", currentDesktop: currentDesktop));

        Assert.Equal(CompositorType.KDE, result);
    }

    private static LinuxEnvironmentSnapshot Environment(
        string? sessionType = null,
        string? waylandDisplay = null,
        string? display = null,
        string? currentDesktop = null) =>
        default(LinuxEnvironmentSnapshot) with
        {
            SessionType = sessionType,
            WaylandDisplay = waylandDisplay,
            Display = display,
            CurrentDesktop = currentDesktop,
        };
}
