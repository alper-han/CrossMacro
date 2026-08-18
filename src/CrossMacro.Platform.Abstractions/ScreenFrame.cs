
namespace CrossMacro.Platform.Abstractions;

public sealed class ScreenFrame : IDisposable
{
    private readonly IDisposable? _owner;
    private readonly ScreenFrameValidityIndex? _validityIndex;
    private bool _disposed;

    public ScreenFrame(
        ScreenRect logicalBounds,
        int stride,
        ScreenPixelFormat pixelFormat,
        ReadOnlyMemory<byte> pixels,
        IDisposable? owner = null,
        ReadOnlyMemory<byte> validPixelMask = default,
        ScreenFrameValidityIndex? validityIndex = null,
        ScreenAlphaMode alphaMode = ScreenAlphaMode.Unknown)
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

        var validPixelCount = checked(logicalBounds.Width * logicalBounds.Height);
        if (!validPixelMask.IsEmpty && validPixelMask.Length < validPixelCount)
        {
            throw new ArgumentException("Screen frame valid-pixel mask is smaller than the declared frame dimensions.", nameof(validPixelMask));
        }

        if (bytesPerPixel is 3 && alphaMode is not (ScreenAlphaMode.Unknown or ScreenAlphaMode.Opaque))
        {
            throw new ArgumentException("RGB screen frames cannot declare an alpha channel.", nameof(alphaMode));
        }

        if (pixelFormat is ScreenPixelFormat.Xrgb8888 or ScreenPixelFormat.Xbgr8888
            && alphaMode is not (ScreenAlphaMode.Unknown or ScreenAlphaMode.Opaque))
        {
            throw new ArgumentException("XRGB screen frames cannot declare an alpha channel.", nameof(alphaMode));
        }

        var normalizedValidPixelMask = validPixelMask.IsEmpty
            || (validityIndex is null && validPixelMask.Span[..validPixelCount].IndexOf((byte)0) < 0)
            ? ReadOnlyMemory<byte>.Empty
            : validPixelMask.Slice(0, validPixelCount);

        LogicalBounds = logicalBounds;
        Stride = stride;
        PixelFormat = pixelFormat;
        Pixels = pixels;
        ValidPixelMask = normalizedValidPixelMask;
        AlphaMode = alphaMode is ScreenAlphaMode.Unknown
            && pixelFormat is ScreenPixelFormat.Rgb24 or ScreenPixelFormat.Bgr24 or ScreenPixelFormat.Xrgb8888 or ScreenPixelFormat.Xbgr8888
            ? ScreenAlphaMode.Opaque
            : alphaMode;
        _owner = owner;
        _validityIndex = validityIndex;
    }

    public ScreenRect LogicalBounds { get; }

    public int Width => LogicalBounds.Width;

    public int Height => LogicalBounds.Height;

    public int Stride { get; }

    public ScreenPixelFormat PixelFormat { get; }

    public ReadOnlyMemory<byte> Pixels { get; }

    public ScreenAlphaMode AlphaMode { get; }

    public bool HasAlphaChannel => PixelFormat is ScreenPixelFormat.Bgra8888 or ScreenPixelFormat.Abgr8888;

    public ReadOnlyMemory<byte> ValidPixelMask { get; }

    public bool HasValidPixelMask => !ValidPixelMask.IsEmpty;

    public bool IsFullyValid => !HasValidPixelMask && _validityIndex is null;

    public bool HasValidityIndex => _validityIndex is not null;

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

        if (!LogicalBounds.Contains(point) || !IsValidPixel(point))
        {
            color = default;
            return false;
        }

        color = ReadPixel(point);
        return true;
    }

    public bool IsPixelValid(ScreenPoint point)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return LogicalBounds.Contains(point) && IsValidPixel(point);
    }

    public bool TryGetAlpha(ScreenPoint point, out byte alpha)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!LogicalBounds.Contains(point) || !IsValidPixel(point) || !HasAlphaChannel)
        {
            alpha = byte.MaxValue;
            return false;
        }

        alpha = ReadAlpha(point);
        return true;
    }

    public bool ContainsAnyValidPixel(ScreenRect region)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!LogicalBounds.Contains(region))
        {
            throw new ArgumentOutOfRangeException(nameof(region), region, "The search region is outside the frame bounds.");
        }

        if (!HasValidPixelMask)
        {
            return _validityIndex?.ContainsAnyValidPixel(region, LogicalBounds) ?? true;
        }

        var mask = ValidPixelMask.Span;
        for (var currentY = region.Y; currentY < region.Bottom; currentY++)
        {
            var maskOffset = checked(((currentY - LogicalBounds.Y) * Width) + region.X - LogicalBounds.X);
            for (var currentX = 0; currentX < region.Width; currentX++)
            {
                if (mask[maskOffset + currentX] is not 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public bool IsRectangleFullyValid(ScreenRect region)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!LogicalBounds.Contains(region))
        {
            throw new ArgumentOutOfRangeException(nameof(region), region, "The search region is outside the frame bounds.");
        }

        if (IsFullyValid)
        {
            return true;
        }

        if (_validityIndex is not null)
        {
            return _validityIndex.IsRectangleFullyValid(region, LogicalBounds);
        }

        var mask = ValidPixelMask.Span;
        for (var currentY = region.Y; currentY < region.Bottom; currentY++)
        {
            var maskOffset = checked(((currentY - LogicalBounds.Y) * Width) + region.X - LogicalBounds.X);
            for (var currentX = 0; currentX < region.Width; currentX++)
            {
                if (mask[maskOffset + currentX] is 0)
                {
                    return false;
                }
            }
        }

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
                if (!IsValidPixel(point))
                {
                    continue;
                }

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
        _validityIndex?.Dispose();
        _owner?.Dispose();
    }

    public static int GetBytesPerPixel(ScreenPixelFormat pixelFormat) => pixelFormat switch
    {
        ScreenPixelFormat.Rgb24 or ScreenPixelFormat.Bgr24 => 3,
        ScreenPixelFormat.Xrgb8888 or ScreenPixelFormat.Bgra8888 or ScreenPixelFormat.Abgr8888 or ScreenPixelFormat.Xbgr8888 => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(pixelFormat), pixelFormat, "Unsupported screen pixel format."),
    };

    private ScreenPixelColor ReadPixel(ScreenPoint point)
    {
        var localX = point.X - LogicalBounds.X;
        var localY = point.Y - LogicalBounds.Y;
        var offset = checked((localY * Stride) + (localX * GetBytesPerPixel(PixelFormat)));
        var span = Pixels.Span;

        return PixelFormat switch
        {
            ScreenPixelFormat.Rgb24 => new ScreenPixelColor(span[offset], span[offset + 1], span[offset + 2]),
            ScreenPixelFormat.Bgr24 => new ScreenPixelColor(span[offset + 2], span[offset + 1], span[offset]),
            ScreenPixelFormat.Xrgb8888 => new ScreenPixelColor(span[offset + 2], span[offset + 1], span[offset]),
            ScreenPixelFormat.Bgra8888 => new ScreenPixelColor(span[offset + 2], span[offset + 1], span[offset]),
            ScreenPixelFormat.Abgr8888 => new ScreenPixelColor(span[offset], span[offset + 1], span[offset + 2]),
            ScreenPixelFormat.Xbgr8888 => new ScreenPixelColor(span[offset], span[offset + 1], span[offset + 2]),
            _ => throw new InvalidOperationException($"Unsupported screen pixel format '{PixelFormat}'."),
        };
    }

    private byte ReadAlpha(ScreenPoint point)
    {
        var localX = point.X - LogicalBounds.X;
        var localY = point.Y - LogicalBounds.Y;
        var offset = checked((localY * Stride) + (localX * GetBytesPerPixel(PixelFormat)));
        var span = Pixels.Span;

        return PixelFormat is ScreenPixelFormat.Bgra8888 or ScreenPixelFormat.Abgr8888
            ? span[offset + 3]
            : byte.MaxValue;
    }

    private bool IsValidPixel(ScreenPoint point)
    {
        if (_validityIndex is not null)
        {
            return _validityIndex.IsPixelValid(point, LogicalBounds);
        }

        if (!HasValidPixelMask)
        {
            return true;
        }

        var localX = point.X - LogicalBounds.X;
        var localY = point.Y - LogicalBounds.Y;
        return ValidPixelMask.Span[checked((localY * Width) + localX)] is not 0;
    }
}
