namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public sealed class WaylandOutputInfoTests
{
    [Theory]
    [InlineData(3840, 2160, 2, 0, 1920, 1080)]
    [InlineData(1920, 1080, 1, 1, 1080, 1920)]
    [InlineData(3000, 2000, 2, 5, 1000, 1500)]
    [InlineData(2560, 1440, 0, 0, 2560, 1440)]
    public void ResolveFallbackLogicalSize_AppliesIntegerScaleAndOutputTransform(
        int modeWidth,
        int modeHeight,
        int scale,
        int transform,
        int expectedWidth,
        int expectedHeight)
    {
        var size = WaylandOutputInfo.ResolveFallbackLogicalSize(
            modeWidth,
            modeHeight,
            scale,
            transform);

        Assert.Equal(expectedWidth, size.Width);
        Assert.Equal(expectedHeight, size.Height);
    }
}
