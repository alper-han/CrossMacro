namespace CrossMacro.Platform.Abstractions;

public sealed class ImageAssetPreview
{
    public ImageAssetPreview(int width, int height, int stride, ReadOnlyMemory<byte> pixels)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        if (stride < checked(width * 4))
        {
            throw new ArgumentOutOfRangeException(nameof(stride));
        }

        if (pixels.Length < checked(stride * height))
        {
            throw new ArgumentException("Preview pixel memory is smaller than the declared dimensions.", nameof(pixels));
        }

        Width = width;
        Height = height;
        Stride = stride;
        Pixels = pixels;
    }

    public int Width { get; }

    public int Height { get; }

    public int Stride { get; }

    public ReadOnlyMemory<byte> Pixels { get; }
}
