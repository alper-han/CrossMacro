
namespace CrossMacro.Infrastructure.Services.ScreenCapture;

public sealed class ImageAssetPreviewDecoder(IImageAssetCodec imageAssetCodec) : IImageAssetPreviewDecoder
{
    public const int MaxPreviewWidth = 640;
    public const int MaxPreviewHeight = 360;

    private readonly IImageAssetCodec _imageAssetCodec = imageAssetCodec ?? throw new ArgumentNullException(nameof(imageAssetCodec));

    public ImageAssetPreview Decode(string encoded, string? assetName = null)
    {
        using var frame = _imageAssetCodec.DecodeBase64Png(encoded, assetName);
        return CreatePreview(frame);
    }

    public async Task<ImageAssetPreview> DecodeAsync(string encoded, string? assetName = null, CancellationToken cancellationToken = default)
    {
        using var frame = await _imageAssetCodec.DecodeBase64PngAsync(encoded, assetName, cancellationToken).ConfigureAwait(false);
        return CreatePreview(frame);
    }

    private static ImageAssetPreview CreatePreview(ScreenFrame frame)
    {
        var (width, height) = GetPreviewSize(frame.Width, frame.Height);
        var stride = checked(width * 4);
        var pixels = new byte[checked(stride * height)];
        var sourcePixels = frame.Pixels.Span;
        var layout = ScreenPixelFormatLayout.Get(frame.PixelFormat);
        var hasAlpha = layout.HasAlphaChannel && frame.AlphaMode is not ScreenAlphaMode.Opaque;
        var isFullyValid = frame.IsFullyValid;

        for (var y = 0; y < height; y++)
        {
            var sourceY = y * frame.Height / height;
            var sourceRowOffset = sourceY * frame.Stride;
            for (var x = 0; x < width; x++)
            {
                var sourceX = x * frame.Width / width;
                var destinationOffset = checked((y * stride) + (x * 4));
                if (!isFullyValid && !frame.IsPixelValid(new ScreenPoint(frame.LogicalBounds.X + sourceX, frame.LogicalBounds.Y + sourceY)))
                {
                    continue;
                }

                var sourceOffset = sourceRowOffset + (sourceX * layout.BytesPerPixel);
                pixels[destinationOffset] = sourcePixels[sourceOffset + layout.BlueOffset];
                pixels[destinationOffset + 1] = sourcePixels[sourceOffset + layout.GreenOffset];
                pixels[destinationOffset + 2] = sourcePixels[sourceOffset + layout.RedOffset];
                pixels[destinationOffset + 3] = hasAlpha ? sourcePixels[sourceOffset + layout.AlphaOffset] : byte.MaxValue;
            }
        }

        return new ImageAssetPreview(width, height, stride, pixels);
    }

    private static (int Width, int Height) GetPreviewSize(int width, int height)
    {
        if (width <= MaxPreviewWidth && height <= MaxPreviewHeight)
        {
            return (width, height);
        }

        var scale = Math.Min((double)MaxPreviewWidth / width, (double)MaxPreviewHeight / height);
        return (
            Math.Max(1, (int)Math.Round(width * scale, MidpointRounding.AwayFromZero)),
            Math.Max(1, (int)Math.Round(height * scale, MidpointRounding.AwayFromZero)));
    }
}
