namespace CrossMacro.Platform.Linux.Tests.PlatformAbstractions;

public sealed class ImageAssetPreviewTests
{
    [Fact]
    public void Constructor_PreservesDimensionsStrideAndReadOnlyMemory()
    {
        var pixels = new byte[12];

        var preview = new ImageAssetPreview(2, 1, 12, pixels);

        Assert.Equal(2, preview.Width);
        Assert.Equal(1, preview.Height);
        Assert.Equal(12, preview.Stride);
        Assert.Equal(pixels, preview.Pixels.ToArray());
    }

    [Theory]
    [InlineData(0, 1, 4, 4)]
    [InlineData(1, 0, 4, 4)]
    [InlineData(1, 1, 3, 3)]
    public void Constructor_RejectsInvalidDimensionsOrStride(int width, int height, int stride, int pixelLength)
    {
        Assert.ThrowsAny<ArgumentException>(() => new ImageAssetPreview(width, height, stride, new byte[pixelLength]));
    }

    [Fact]
    public void Constructor_RejectsInsufficientPixelMemory()
    {
        var exception = Assert.Throws<ArgumentException>(() => new ImageAssetPreview(2, 2, 8, new byte[15]));

        Assert.Equal("pixels", exception.ParamName);
    }
}
