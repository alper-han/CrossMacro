using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.DisplayServer.Wayland;

namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading;

public sealed class PortalStreamGeometryTests
{
    [Fact]
    public void ValidateMonitorStreams_WhenMonitorHasNegativePosition_ReturnsLogicalBounds()
    {
        var stream = Stream(42, id: "left", x: -1920, y: 0, width: 1920, height: 1080);

        var result = PortalStreamGeometry.ValidateMonitorStreams([stream]);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ScreenRect(-1920, 0, 1920, 1080), result.Stream.Bounds);
    }

    [Fact]
    public void ValidateMonitorStreams_WhenSourceIsNotMonitor_ReturnsCaptureFailed()
    {
        var stream = Stream(42, sourceType: 2U);

        var result = PortalStreamGeometry.ValidateMonitorStreams([stream]);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.CaptureFailed, result.ErrorKind);
        Assert.Contains("non-monitor", result.ErrorMessage);
        Assert.Contains("cannot force GNOME portal", result.ErrorMessage);
    }

    [Fact]
    public void ValidateMonitorStreams_WhenPositionIsMissing_ReturnsCaptureFailed()
    {
        var stream = Stream(42, includePosition: false);

        var result = PortalStreamGeometry.ValidateMonitorStreams([stream]);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.CaptureFailed, result.ErrorKind);
        Assert.Contains("position", result.ErrorMessage);
    }

    [Theory]
    [InlineData(0, 1080)]
    [InlineData(1920, 0)]
    public void ValidateMonitorStreams_WhenSizeIsNotPositive_ReturnsCaptureFailed(int width, int height)
    {
        var stream = Stream(42, width: width, height: height);

        var result = PortalStreamGeometry.ValidateMonitorStreams([stream]);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.CaptureFailed, result.ErrorKind);
        Assert.Contains("positive size", result.ErrorMessage);
    }

    [Fact]
    public void ValidateMonitorStreams_WhenNonblankIdsDuplicate_ReturnsCaptureFailed()
    {
        var first = Stream(42, id: "same", x: 0, y: 0);
        var second = Stream(43, id: "same", x: 1920, y: 0);

        var result = PortalStreamGeometry.ValidateMonitorStreams([first, second]);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.CaptureFailed, result.ErrorKind);
        Assert.Contains("duplicate monitor stream id", result.ErrorMessage);
    }

    [Fact]
    public void ValidateMonitorStreams_WhenBlankIdsDuplicate_AllowsStreams()
    {
        var first = Stream(42, id: null, x: 0, y: 0);
        var second = Stream(43, id: null, x: 1920, y: 0);

        var result = PortalStreamGeometry.ValidateMonitorStreams([first, second]);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ScreenRect(0, 0, 3840, 1080), result.SelectedBounds);
    }

    [Fact]
    public void ValidateMonitorStreams_WhenBoundsDuplicate_ReturnsCaptureFailed()
    {
        var first = Stream(42, id: "first", x: 0, y: 0);
        var second = Stream(43, id: "second", x: 0, y: 0);

        var result = PortalStreamGeometry.ValidateMonitorStreams([first, second]);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.CaptureFailed, result.ErrorKind);
        Assert.Contains("duplicate monitor stream bounds", result.ErrorMessage);
    }

    [Fact]
    public void ValidateMonitorStreams_WhenRequestedRegionFallsInGap_ReturnsOutOfBounds()
    {
        var first = Stream(42, id: "first", x: 0, y: 0, width: 100, height: 100);
        var second = Stream(43, id: "second", x: 200, y: 0, width: 100, height: 100);

        var result = PortalStreamGeometry.ValidateMonitorStreams([first, second], new ScreenRect(150, 10, 1, 1));

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.OutOfBounds, result.ErrorKind);
        Assert.Contains("cannot force GNOME portal", result.ErrorMessage);
    }

    private static PortalStream Stream(
        uint nodeId,
        string? id = "monitor",
        uint sourceType = 1U,
        int x = 0,
        int y = 0,
        int width = 1920,
        int height = 1080,
        bool includePosition = true)
    {
        var properties = new Dictionary<string, object>
        {
            ["source_type"] = sourceType,
            ["size"] = new object[] { width, height }
        };

        if (includePosition)
        {
            properties["position"] = new object[] { x, y };
        }

        if (id is not null)
        {
            properties["id"] = id;
        }

        return new PortalStream(nodeId, properties);
    }
}
