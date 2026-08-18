
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

        for (var y = 0; y < height; y++)
        {
            var sourceY = frame.LogicalBounds.Y + (y * frame.Height / height);
            for (var x = 0; x < width; x++)
            {
                var sourceX = frame.LogicalBounds.X + (x * frame.Width / width);
                var destinationOffset = checked((y * stride) + (x * 4));
                if (!frame.TryGetPixel(new ScreenPoint(sourceX, sourceY), out var color))
                {
                    continue;
                }

                pixels[destinationOffset] = color.B;
                pixels[destinationOffset + 1] = color.G;
                pixels[destinationOffset + 2] = color.R;
                pixels[destinationOffset + 3] = byte.MaxValue;
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
