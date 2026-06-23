using System;

namespace CrossMacro.Platform.Abstractions;

public sealed class ScreenFrame : IDisposable
{
    private readonly IDisposable? _owner;
    private bool _disposed;

    public ScreenFrame(ScreenRect logicalBounds, int stride, ScreenPixelFormat pixelFormat, ReadOnlyMemory<byte> pixels, IDisposable? owner = null)
    {
        var bytesPerPixel = GetBytesPerPixel(pixelFormat);
        var minimumStride = checked(logicalBounds.Width * bytesPerPixel);

        if (stride < minimumStride)
        {
            throw new ArgumentOutOfRangeException(nameof(stride), stride, "Screen frame stride is smaller than the logical row width.");
        }

        var minimumLength = checked(stride * logicalBounds.Height);
        if (pixels.Length < minimumLength)
        {
            throw new ArgumentException("Screen frame pixel memory is smaller than the declared frame dimensions.", nameof(pixels));
        }

        LogicalBounds = logicalBounds;
        Stride = stride;
        PixelFormat = pixelFormat;
        Pixels = pixels;
        _owner = owner;
    }

    public ScreenRect LogicalBounds { get; }

    public int Width => LogicalBounds.Width;

    public int Height => LogicalBounds.Height;

    public int Stride { get; }

    public ScreenPixelFormat PixelFormat { get; }

    public ReadOnlyMemory<byte> Pixels { get; }

    public ScreenPixelColor GetPixel(ScreenPoint point)
    {
        if (!TryGetPixel(point, out var color))
        {
            throw new ArgumentOutOfRangeException(nameof(point), point, "The screen point is outside the frame bounds.");
        }

        return color;
    }

    public bool TryGetPixel(ScreenPoint point, out ScreenPixelColor color)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!LogicalBounds.Contains(point))
        {
            color = default;
            return false;
        }

        color = ReadPixel(point);
        return true;
    }

    public ScreenPixelSearchMatch? SearchPixel(ScreenRect region, ScreenPixelColor expected, int tolerance = 0)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (tolerance is < 0 or > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance), tolerance, "Screen pixel tolerance must be between 0 and 255.");
        }

        if (!LogicalBounds.Contains(region))
        {
            throw new ArgumentOutOfRangeException(nameof(region), region, "The search region is outside the frame bounds.");
        }

        for (var currentY = region.Y; currentY < region.Bottom; currentY++)
        {
            for (var currentX = region.X; currentX < region.Right; currentX++)
            {
                var point = new ScreenPoint(currentX, currentY);
                var color = ReadPixel(point);
                if (color.IsWithinTolerance(expected, tolerance))
                {
                    return new ScreenPixelSearchMatch(point, color);
                }
            }
        }

        return null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _owner?.Dispose();
    }

    public static int GetBytesPerPixel(ScreenPixelFormat pixelFormat) => pixelFormat switch
    {
        ScreenPixelFormat.Rgb24 or ScreenPixelFormat.Bgr24 => 3,
        ScreenPixelFormat.Xrgb8888 or ScreenPixelFormat.Bgra8888 or ScreenPixelFormat.Abgr8888 or ScreenPixelFormat.Xbgr8888 => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(pixelFormat), pixelFormat, "Unsupported screen pixel format.")
    };

    private ScreenPixelColor ReadPixel(ScreenPoint point)
    {
        var localX = point.X - LogicalBounds.X;
        var localY = point.Y - LogicalBounds.Y;
        var offset = checked(localY * Stride + localX * GetBytesPerPixel(PixelFormat));
        var span = Pixels.Span;

        return PixelFormat switch
        {
            ScreenPixelFormat.Rgb24 => new ScreenPixelColor(span[offset], span[offset + 1], span[offset + 2]),
            ScreenPixelFormat.Bgr24 => new ScreenPixelColor(span[offset + 2], span[offset + 1], span[offset]),
            ScreenPixelFormat.Xrgb8888 => new ScreenPixelColor(span[offset + 2], span[offset + 1], span[offset]),
            ScreenPixelFormat.Bgra8888 => new ScreenPixelColor(span[offset + 2], span[offset + 1], span[offset]),
            ScreenPixelFormat.Abgr8888 => new ScreenPixelColor(span[offset], span[offset + 1], span[offset + 2]),
            ScreenPixelFormat.Xbgr8888 => new ScreenPixelColor(span[offset], span[offset + 1], span[offset + 2]),
            _ => throw new InvalidOperationException($"Unsupported screen pixel format '{PixelFormat}'.")
        };
    }
}
