
namespace CrossMacro.Platform.Abstractions;

public sealed class ScreenFrameValidityIndex : IDisposable
{
    private int[]? _prefix;
    private readonly int _prefixWidth;
    private readonly ArrayPool<int> _pool;

    private ScreenFrameValidityIndex(int[] prefix, int width, ArrayPool<int> pool)
    {
        _prefix = prefix;
        _prefixWidth = checked(width + 1);
        _pool = pool;
    }

    public static ScreenFrameValidityIndex Create(ReadOnlySpan<byte> validPixelMask, int width, int height)
        => Create(validPixelMask, width, height, ArrayPool<int>.Shared);

    internal static ScreenFrameValidityIndex Create(ReadOnlySpan<byte> validPixelMask, int width, int height, ArrayPool<int> pool)
    {
        ArgumentNullException.ThrowIfNull(pool);

        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "Frame width must be positive.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "Frame height must be positive.");
        }

        var pixelCount = checked(width * height);
        if (validPixelMask.Length < pixelCount)
        {
            throw new ArgumentException("The valid-pixel mask is smaller than the declared frame dimensions.", nameof(validPixelMask));
        }

        var prefix = pool.Rent(checked((width + 1) * (height + 1)));
        try
        {
            var prefixWidth = width + 1;
            Array.Clear(prefix, 0, prefixWidth);
            for (var row = 1; row <= height; row++)
            {
                var rowSum = 0;
                var maskOffset = checked((row - 1) * width);
                var prefixOffset = checked(row * prefixWidth);
                var previousOffset = checked((row - 1) * prefixWidth);
                prefix[prefixOffset] = 0;
                for (var column = 1; column <= width; column++)
                {
                    rowSum += validPixelMask[maskOffset + column - 1] is 0 ? 1 : 0;
                    prefix[prefixOffset + column] = prefix[previousOffset + column] + rowSum;
                }
            }

            return new ScreenFrameValidityIndex(prefix, width, pool);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            pool.Return(prefix);
            throw;
        }
    }

    public bool IsRectangleFullyValid(ScreenRect region, ScreenRect logicalBounds)
    {
        return CountInvalidPixels(region, logicalBounds) is 0;
    }

    public bool IsPixelValid(ScreenPoint point, ScreenRect logicalBounds)
    {
        return CountInvalidPixels(new ScreenRect(point.X, point.Y, 1, 1), logicalBounds) is 0;
    }

    public bool ContainsAnyValidPixel(ScreenRect region, ScreenRect logicalBounds)
    {
        var pixelCount = checked(region.Width * region.Height);
        return CountInvalidPixels(region, logicalBounds) < pixelCount;
    }

    public void Dispose()
    {
        var prefix = _prefix;
        if (prefix is null)
        {
            return;
        }

        _prefix = null;
        _pool.Return(prefix);
    }

    private int CountInvalidPixels(ScreenRect region, ScreenRect logicalBounds)
    {
        var prefix = _prefix ?? throw new ObjectDisposedException(nameof(ScreenFrameValidityIndex));
        var left = checked(region.X - logicalBounds.X);
        var top = checked(region.Y - logicalBounds.Y);
        var right = checked(left + region.Width);
        var bottom = checked(top + region.Height);
        var bottomOffset = checked(bottom * _prefixWidth);
        var topOffset = checked(top * _prefixWidth);
        return prefix[bottomOffset + right]
            - prefix[topOffset + right]
            - prefix[bottomOffset + left]
            + prefix[topOffset + left];
    }
}
