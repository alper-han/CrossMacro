namespace CrossMacro.Infrastructure.Tests.TestDoubles;

internal static class TestScreenFrames
{
    internal static readonly ScreenPixelColor Black = new(0x00, 0x00, 0x00);
    internal static readonly ScreenPixelColor Red = new(0xFF, 0x00, 0x00);
    internal static readonly ScreenPixelColor Green = new(0x00, 0xFF, 0x00);
    internal static readonly ScreenPixelColor Blue = new(0x00, 0x00, 0xFF);
    internal static readonly ScreenPixelColor White = new(0xFF, 0xFF, 0xFF);

    internal static ScreenPixelColor[][] Solid(int width, int height, ScreenPixelColor color)
    {
        var rows = new ScreenPixelColor[height][];
        for (var y = 0; y < height; y++)
        {
            rows[y] = new ScreenPixelColor[width];
            Array.Fill(rows[y], color);
        }

        return rows;
    }

    internal static ScreenFrame CreateRgbFrame(ScreenRect bounds, ScreenPixelColor[][] pixels, byte[]? validPixelMask = null)
    {
        var stride = bounds.Width * 3;
        var bytes = new byte[stride * bounds.Height];
        for (var y = 0; y < bounds.Height; y++)
        {
            for (var x = 0; x < bounds.Width; x++)
            {
                var offset = (y * stride) + (x * 3);
                bytes[offset] = pixels[y][x].R;
                bytes[offset + 1] = pixels[y][x].G;
                bytes[offset + 2] = pixels[y][x].B;
            }
        }

        var mask = validPixelMask is null ? ReadOnlyMemory<byte>.Empty : validPixelMask;
        return new ScreenFrame(bounds, stride, ScreenPixelFormat.Rgb24, bytes, validPixelMask: mask);
    }
}
