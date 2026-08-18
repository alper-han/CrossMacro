namespace CrossMacro.Platform.Linux.Tests.Native.UInput;

public sealed class UInputDeviceCoordinatePolicyTests
{
    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(1920, 0, false)]
    [InlineData(0, 1080, false)]
    [InlineData(1920, 1080, true)]
    public void SupportsAbsoluteCoordinates_RequiresBothDimensions(int width, int height, bool expected)
    {
        Assert.Equal(expected, UInputDeviceCoordinatePolicy.SupportsAbsoluteCoordinates(width, height));
    }

    [Fact]
    public void ClampAbsoluteCoordinates_PreservesExistingPerDimensionBounds()
    {
        Assert.Equal((0, 1079), UInputDeviceCoordinatePolicy.ClampAbsoluteCoordinates(-10, 2000, 1920, 1080));
        Assert.Equal((10, 20), UInputDeviceCoordinatePolicy.ClampAbsoluteCoordinates(10, 20, 0, 0));
        Assert.Equal((1919, 0), UInputDeviceCoordinatePolicy.ClampAbsoluteCoordinates(2000, -1, 1920, 1080));
    }

    [Theory]
    [InlineData(1920, 1080, 1919, 1079)]
    [InlineData(0, 0, 0, 0)]
    public void GetAbsoluteMaximums_MatchesVirtualDeviceDefinition(int width, int height, int expectedX, int expectedY)
    {
        Assert.Equal((expectedX, expectedY), UInputDeviceCoordinatePolicy.GetAbsoluteMaximums(width, height));
    }

    [Fact]
    public void CreateAbsoluteMovePlan_WhenTargetChanges_DoesNotReassert()
    {
        var plan = UInputDeviceCoordinatePolicy.CreateAbsoluteMovePlan((10, 20), (30, 40), 1920, 1080);

        Assert.Equal((30, 40), plan.Target);
        Assert.Null(plan.Reassertion);
    }

    [Theory]
    [InlineData(10, 20, 1920, 1080, 11, 20)]
    [InlineData(1919, 1079, 1920, 1080, 1918, 1079)]
    [InlineData(0, 1, 1, 3, 0, 2)]
    [InlineData(0, 2, 1, 3, 0, 1)]
    public void CreateAbsoluteMovePlan_WhenTargetRepeats_ReassertsWithinDeviceBounds(
        int x,
        int y,
        int width,
        int height,
        int expectedX,
        int expectedY)
    {
        var plan = UInputDeviceCoordinatePolicy.CreateAbsoluteMovePlan((x, y), (x, y), width, height);

        Assert.Equal((x, y), plan.Target);
        Assert.Equal((expectedX, expectedY), plan.Reassertion);
    }

    [Fact]
    public void CreateAbsoluteMovePlan_WhenDeviceHasNoAdjacentPoint_DoesNotReassert()
    {
        var plan = UInputDeviceCoordinatePolicy.CreateAbsoluteMovePlan((0, 0), (0, 0), 1, 1);

        Assert.Null(plan.Reassertion);
    }
}
