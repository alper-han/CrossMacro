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

    [Theory]
    [InlineData(1)]
    public void ValidateEncodedSize_WhenWithinBounds_DoesNotThrow(int byteCount)
    {
        var act = () => ScreenImageAssetPolicy.ValidateEncodedSize(byteCount);

        act.Should().NotThrow();
        ScreenImageAssetPolicy.ValidateEncodedSize(ScreenImageAssetPolicy.MaxEncodedBytes);
    }

    [Theory]
    [InlineData(0)]
    public void ValidateEncodedSize_WhenEmpty_Throws(int byteCount)
    {
        var act = () => ScreenImageAssetPolicy.ValidateEncodedSize(byteCount, "asset.png");

        act.Should().Throw<InvalidDataException>()
            .WithMessage("Image asset 'asset.png': Image asset is empty.");
    }

    [Fact]
    public void ValidateEncodedSize_WhenOverLimit_Throws()
    {
        var act = () => ScreenImageAssetPolicy.ValidateEncodedSize(ScreenImageAssetPolicy.MaxEncodedBytes + 1);

        act.Should().Throw<InvalidDataException>()
            .WithMessage($"Image asset exceeds the maximum encoded size of {ScreenImageAssetPolicy.MaxEncodedBytes} bytes.");
    }

    [Theory]
    [InlineData(1, 1)]
    public void ValidateDimensions_WhenWithinBounds_DoesNotThrow(int width, int height)
    {
        var act = () => ScreenImageAssetPolicy.ValidateDimensions(width, height);

        act.Should().NotThrow();
        ScreenImageAssetPolicy.ValidateDimensions(ScreenImageAssetPolicy.MaxWidth, ScreenImageAssetPolicy.MaxHeight);
    }

    [Fact]
    public void ValidateDimensions_WhenNonPositive_Throws()
    {
        var act = () => ScreenImageAssetPolicy.ValidateDimensions(0, 1, "asset.png");

        act.Should().Throw<InvalidDataException>()
            .WithMessage("Image asset 'asset.png': Image dimensions must be positive.");
    }

    [Fact]
    public void ValidateDimensions_WhenWidthExceedsLimit_Throws()
    {
        var act = () => ScreenImageAssetPolicy.ValidateDimensions(ScreenImageAssetPolicy.MaxWidth + 1, 1);

        act.Should().Throw<InvalidDataException>()
            .WithMessage($"Image dimensions exceed the maximum supported size of {ScreenImageAssetPolicy.MaxWidth}x{ScreenImageAssetPolicy.MaxHeight}.");
    }

    [Fact]
    public void ValidateMacroBudget_WhenOverLimit_Throws()
    {
        var act = () => ScreenImageAssetPolicy.ValidateMacroBudget(ScreenImageAssetPolicy.MaxMacroEncodedBytes + 1);

        act.Should().Throw<InvalidDataException>()
            .WithMessage($"Macro image assets exceed the maximum combined encoded size of {ScreenImageAssetPolicy.MaxMacroEncodedBytes} bytes.");
    }
}
