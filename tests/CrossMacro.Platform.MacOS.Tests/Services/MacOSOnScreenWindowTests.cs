namespace CrossMacro.Platform.MacOS.Tests.Services;

public sealed class MacOSOnScreenWindowTests
{
    private static readonly ScreenRect Frame = new(10, 20, 300, 200);

    [Fact]
    public void GetApplicationPids_ReturnsDistinctPositivePidsInOrder()
    {
        MacOSOnScreenWindow[] snapshot =
        [
            Window(pid: 30, number: 1),
            Window(pid: 10, number: 2),
            Window(pid: 30, number: 3),
            Window(pid: null, number: 4),
            Window(pid: 0, number: 5),
        ];

        Assert.Equal([30, 10], MacOSOnScreenWindow.GetApplicationPids(snapshot));
    }

    [Fact]
    public void FindWindowId_MatchesPidLayerBoundsAndTitle()
    {
        MacOSOnScreenWindow[] snapshot =
        [
            Window(pid: 1, number: 11, name: "Title"),
            Window(pid: 2, number: 12, name: "Title"),
            Window(pid: 1, number: 13, layer: 25, name: "Title"),
            Window(pid: 1, number: 14, bounds: new ScreenRect(0, 0, 5, 5), name: "Title"),
            Window(pid: 1, number: 15, name: "Other"),
        ];

        Assert.Equal(11u, MacOSOnScreenWindow.FindWindowId(snapshot, pid: 1, "Title", Frame));
    }

    [Fact]
    public void FindWindowId_AcceptsWindowsWithoutNativeTitle()
    {
        MacOSOnScreenWindow[] snapshot = [Window(pid: 1, number: 11)];

        Assert.Equal(11u, MacOSOnScreenWindow.FindWindowId(snapshot, pid: 1, "Title", Frame));
    }

    [Fact]
    public void FindWindowId_RejectsAmbiguousMatches()
    {
        MacOSOnScreenWindow[] snapshot = [Window(pid: 1, number: 11, name: "Title"), Window(pid: 1, number: 12)];

        Assert.Null(MacOSOnScreenWindow.FindWindowId(snapshot, pid: 1, "Title", Frame));
    }

    [Fact]
    public void FindWindowId_RejectsUnnumberedMatches()
    {
        MacOSOnScreenWindow[] snapshot = [Window(pid: 1, number: null, name: "Title")];

        Assert.Null(MacOSOnScreenWindow.FindWindowId(snapshot, pid: 1, "Title", Frame));
    }

    private static MacOSOnScreenWindow Window(
        int? pid,
        int? number,
        int? layer = 0,
        ScreenRect? bounds = null,
        string? name = null) =>
        new(pid, layer, number, bounds ?? Frame, name);
}
