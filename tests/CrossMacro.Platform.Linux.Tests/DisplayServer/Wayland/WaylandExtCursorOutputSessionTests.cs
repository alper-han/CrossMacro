namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public sealed class WaylandExtCursorOutputSessionTests
{
    [Theory]
    [InlineData(0, 0, -1920, -200)]
    [InlineData(3839, 2159, -1, 879)]
    [InlineData(1920, 1080, -960, 340)]
    public void MapCursorPosition_ShouldMapTransformedBufferPixelsToLogicalDesktop(
        int bufferX,
        int bufferY,
        int expectedX,
        int expectedY)
    {
        var position = WaylandExtCursorOutputSession.MapCursorPosition(
            new ScreenRect(-1920, -200, 1920, 1080),
            bufferWidth: 3840,
            bufferHeight: 2160,
            bufferX,
            bufferY);

        Assert.Equal((expectedX, expectedY), position);
    }

    [Theory]
    [InlineData(-1, 0, -1, 0)]
    [InlineData(0, -1, 0, -1)]
    [InlineData(3840, 0, 1920, 0)]
    [InlineData(0, 2160, 0, 1080)]
    public void MapCursorPosition_ShouldAllowCursorHotspotsOutsideBuffer(
        int bufferX,
        int bufferY,
        int expectedX,
        int expectedY)
    {
        var position = WaylandExtCursorOutputSession.MapCursorPosition(
            new ScreenRect(0, 0, 1920, 1080),
            bufferWidth: 3840,
            bufferHeight: 2160,
            bufferX,
            bufferY);

        Assert.Equal((expectedX, expectedY), position);
    }

    [Fact]
    public void MapCursorPosition_ShouldRejectMissingBufferGeometry()
    {
        var position = WaylandExtCursorOutputSession.MapCursorPosition(
            new ScreenRect(0, 0, 1920, 1080),
            bufferWidth: 0,
            bufferHeight: 1080,
            bufferX: 0,
            bufferY: 0);

        Assert.Null(position);
    }

    [Fact]
    public void MapCursorPosition_ShouldRejectLogicalCoordinateOverflow()
    {
        var position = WaylandExtCursorOutputSession.MapCursorPosition(
            new ScreenRect(int.MaxValue - 10, 0, 20, 20),
            bufferWidth: 20,
            bufferHeight: 20,
            bufferX: 19,
            bufferY: 0);

        Assert.Null(position);
    }
}
