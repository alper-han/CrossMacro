using CrossMacro.Platform.Abstractions.ScreenReading;

namespace CrossMacro.Cli.Tests;

public class ScreenRegionRulesTests
{
    [Fact]
    public void TryCreate_WhenNoComponentIsGiven_ReturnsNoRegion()
    {
        Assert.True(ScreenRegionRules.TryCreate(x: null, y: null, width: null, height: null, "--region", out var region, out var error));
        Assert.Null(region);
        Assert.Null(error);
    }

    [Fact]
    public void TryCreate_WhenComponentsAreComplete_ReturnsRegion()
    {
        Assert.True(ScreenRegionRules.TryCreate(1, -2, 30, 40, "--region", out var region, out _));
        Assert.Equal(new ScreenRect(1, -2, 30, 40), region);
    }

    [Fact]
    public void TryCreate_WhenComponentsArePartial_ReportsMissingOperands()
    {
        Assert.False(ScreenRegionRules.TryCreate(1, 2, width: null, height: null, "screen --region", out var region, out var error));
        Assert.Null(region);
        Assert.Equal("screen --region requires <x> <y> <width> <height>.", error);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    [InlineData(-5, 10)]
    public void Validate_WhenSizeIsNotPositive_ReportsSize(int width, int height)
    {
        Assert.Equal("--region width and height must be positive.", ScreenRegionRules.Validate(0, 0, width, height, "--region"));
    }

    [Theory]
    [InlineData(int.MaxValue, 0, 1, 1)]
    [InlineData(0, int.MaxValue, 1, 1)]
    [InlineData(2, 2, int.MaxValue, 1)]
    public void Validate_WhenEndpointOverflows_ReportsRange(int x, int y, int width, int height)
    {
        Assert.Equal("--region endpoint exceeds the supported screen coordinate range.", ScreenRegionRules.Validate(x, y, width, height, "--region"));
    }

    [Fact]
    public void Validate_WhenWidthExceedsIntRange_ReportsRange()
    {
        Assert.Equal(
            "Screen region endpoint exceeds the supported screen coordinate range.",
            ScreenRegionRules.Validate(0, 0, (long)int.MaxValue + 1, 1, "Screen region"));
    }

    [Fact]
    public void Validate_WhenRegionFitsExactly_ReturnsNull()
    {
        Assert.Null(ScreenRegionRules.Validate(0, 0, int.MaxValue, 1, "--region"));
    }
}
