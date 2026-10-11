namespace CrossMacro.Infrastructure.Tests.Services.ScreenCapture;

public sealed class ImageAssetPreviewDecoderTests
{
    [Fact]
    public async Task DecodeAsync_PreservesTransparentAndTranslucentPixels()
    {
        using var source = new ScreenFrame(
            new ScreenRect(0, 0, 4, 1),
            stride: 16,
            ScreenPixelFormat.Abgr8888,
            new byte[]
            {
                0x12, 0x34, 0x56, 0x00,
                0x23, 0x45, 0x67, 0x01,
                0x34, 0x56, 0x78, 0x80,
                0x45, 0x67, 0x89, 0xFF,
            },
            alphaMode: ScreenAlphaMode.Straight);
        var codec = new ImageAssetCodec();
        using var png = new MemoryStream();
        await codec.EncodePngAsync(source, png, TestContext.Current.CancellationToken);

        var preview = await new ImageAssetPreviewDecoder(codec).DecodeAsync(Convert.ToBase64String(png.ToArray()), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(new byte[]
        {
            0x56, 0x34, 0x12, 0x00,
            0x67, 0x45, 0x23, 0x01,
            0x78, 0x56, 0x34, 0x80,
            0x89, 0x67, 0x45, 0xFF,
        }, preview.Pixels.ToArray());
    }

    [Fact]
    public async Task DecodeAsync_WhenDownscaled_PreservesSampledAlpha()
    {
        const int width = 1280;
        const int height = 720;
        var pixels = new byte[width * height * 4];
        pixels[1] = 0xFF;
        pixels[3] = 0xFF;
        var translucentOffset = ((2 * width) + 2) * 4;
        pixels[translucentOffset + 2] = 0xFF;
        pixels[translucentOffset + 3] = 0x80;
        var lastOffset = ((718 * width) + 1278) * 4;
        pixels[lastOffset] = 0xFF;
        pixels[lastOffset + 3] = 0x01;
        using var source = new ScreenFrame(
            new ScreenRect(0, 0, width, height),
            stride: width * 4,
            ScreenPixelFormat.Abgr8888,
            pixels,
            alphaMode: ScreenAlphaMode.Straight);
        var codec = new ImageAssetCodec();
        using var png = new MemoryStream();
        await codec.EncodePngAsync(source, png, TestContext.Current.CancellationToken);

        var preview = await new ImageAssetPreviewDecoder(codec).DecodeAsync(Convert.ToBase64String(png.ToArray()), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(640, preview.Width);
        Assert.Equal(360, preview.Height);
        Assert.Equal(new byte[] { 0x00, 0xFF, 0x00, 0xFF }, preview.Pixels[..4].ToArray());
        Assert.Equal(new byte[] { 0xFF, 0x00, 0x00, 0x80 }, preview.Pixels.Slice(preview.Stride + 4, 4).ToArray());
        Assert.Equal(new byte[] { 0x00, 0x00, 0x00, 0x00 }, preview.Pixels.Slice(preview.Stride + 8, 4).ToArray());
        Assert.Equal(new byte[] { 0x00, 0x00, 0xFF, 0x01 }, preview.Pixels[^4..].ToArray());
    }

    [Fact]
    public async Task DecodeAsync_WhenPngHasNoAlphaChannel_ProducesOpaquePixels()
    {
        using var source = new ScreenFrame(
            new ScreenRect(0, 0, 1, 1),
            stride: 3,
            ScreenPixelFormat.Rgb24,
            new byte[] { 0x12, 0x34, 0x56 });
        var codec = new ImageAssetCodec();
        using var png = new MemoryStream();
        await codec.EncodePngAsync(source, png, TestContext.Current.CancellationToken);

        var preview = await new ImageAssetPreviewDecoder(codec).DecodeAsync(Convert.ToBase64String(png.ToArray()), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(new byte[] { 0x56, 0x34, 0x12, 0xFF }, preview.Pixels.ToArray());
    }
}
