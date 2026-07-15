
namespace CrossMacro.Platform.Abstractions;

public sealed class ScreenFrameValidityIndex : IDisposable
{
    private int[]? _prefix;
    private readonly int _prefixWidth;

    private ScreenFrameValidityIndex(int[] prefix, int width, int height)
    {
        _prefix = prefix;
        _prefixWidth = checked(width + 1);
    }

    public static ScreenFrameValidityIndex Create(ReadOnlySpan<byte> validPixelMask, int width, int height)
    {
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

        var prefix = ArrayPool<int>.Shared.Rent(checked((width + 1) * (height + 1)));
        try
        {
            var prefixWidth = width + 1;
            for (var row = 1; row <= height; row++)
            {
                var rowSum = 0;
                var maskOffset = checked((row - 1) * width);
                var prefixOffset = checked(row * prefixWidth);
                var previousOffset = checked((row - 1) * prefixWidth);
                for (var column = 1; column <= width; column++)
                {
                    rowSum += validPixelMask[maskOffset + column - 1] is 0 ? 1 : 0;
                    prefix[prefixOffset + column] = prefix[previousOffset + column] + rowSum;
                }
            }

            return new ScreenFrameValidityIndex(prefix, width, height);
        }
        catch
        {
            ArrayPool<int>.Shared.Return(prefix);
            throw;
        }
    }

    public bool IsRectangleFullyValid(ScreenRect region, ScreenRect logicalBounds)
    {
        var prefix = _prefix ?? throw new ObjectDisposedException(nameof(ScreenFrameValidityIndex));
        var left = checked(region.X - logicalBounds.X);
        var top = checked(region.Y - logicalBounds.Y);
        var right = checked(left + region.Width);
        var bottom = checked(top + region.Height);
        var bottomOffset = checked(bottom * _prefixWidth);
        var topOffset = checked(top * _prefixWidth);
        var invalidCount = prefix[bottomOffset + right]
            - prefix[topOffset + right]
            - prefix[bottomOffset + left]
            + prefix[topOffset + left];
        return invalidCount is 0;
    }

    public void Dispose()
    {
        var prefix = _prefix;
        if (prefix is null)
        {
            return;
        }

        _prefix = null;
        ArrayPool<int>.Shared.Return(prefix);
    }
}
