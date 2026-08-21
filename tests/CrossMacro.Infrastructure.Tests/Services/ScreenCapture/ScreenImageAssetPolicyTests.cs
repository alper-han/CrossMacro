namespace CrossMacro.Infrastructure.Tests.Services.ScreenCapture;

public sealed class ScreenImageAssetPolicyTests
{
    [Fact]
    public void GenericEncodedPngBudget_ShouldBeOwnedByThePlatformContract()
    {
        Assert.Equal(
            ScreenshotPngCaptureLimits.MaximumEncodedBytes,
            ScreenImageAssetPolicy.MaxEncodedBytes);
        Assert.Equal(
            ScreenshotPngCaptureLimits.MaximumEncodedBytes,
            ScreenshotPngCaptureRequest.DefaultMaximumEncodedBytes);
    }
}
