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
}
