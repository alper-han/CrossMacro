namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public sealed class HyprlandPositionProviderTests
{
    [Fact]
    public void ParseMonitorBounds_ShouldPreserveNegativeOriginAndLogicalScale()
    {
        const string response = """
            Monitor DP-1 (ID 0):
                3840x2160@144.00000 at -1920x-200
                scale: 2.00
            Monitor DP-2 (ID 1):
                2560x1440@144.00000 at 0x0
                scale: 1.00
            """;

        var bounds = HyprlandPositionProvider.ParseMonitorBounds(response);

        Assert.Equal(new ScreenRect(-1920, -200, 4480, 1640), bounds);
    }

    [Fact]
    public void ParseMonitorBounds_ShouldApplyQuarterTurnTransformBeforeLogicalScale()
    {
        const string response = """
            Monitor DP-1 (ID 0):
                3840x2160@144.00000 at -1080x0
                scale: 2.00
                transform: 1
            Monitor DP-2 (ID 1):
                1920x1080@144.00000 at 0x0
                scale: 1.00
                transform: 0
            """;

        var bounds = HyprlandPositionProvider.ParseMonitorBounds(response);

        Assert.Equal(new ScreenRect(-1080, 0, 3000, 1920), bounds);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("NaN")]
    public void ParseMonitorBounds_ShouldRejectInvalidScale(string scale)
    {
        string response = $"""
            Monitor DP-1 (ID 0):
                1920x1080@60.00000 at 0x0
                scale: {scale}
            """;

        Assert.Null(HyprlandPositionProvider.ParseMonitorBounds(response));
    }
}
