using static CrossMacro.Infrastructure.Services.ScreenReading.ImageMatching.MatcherWork;

namespace CrossMacro.Infrastructure.Services.ScreenReading.ImageMatching;

/// <summary>Converts screen frames of any pixel format into <see cref="RgbImage"/> buffers.</summary>
internal static class FrameNormalization
{
    internal static RgbImage NormalizeFrame(ScreenFrame frame, bool useAlphaMask, byte alphaThreshold, CancellationToken cancellationToken)
    {
        var target = new byte[checked(frame.Width * frame.Height * ColorChannelCount)];
        var requiresAlphaMask = useAlphaMask
            && (frame.AlphaMode is ScreenAlphaMode.Straight or ScreenAlphaMode.Premultiplied);
        var requiresValidityMask = !frame.IsFullyValid;
        var alphaMask = requiresAlphaMask || requiresValidityMask
            ? new byte[checked(frame.Width * frame.Height)]
            : null;
        NormalizeFrameInto(frame, target, alphaMask, requiresAlphaMask, frame.LogicalBounds, alphaThreshold, cancellationToken);
        return CreateRgbImage(frame.Width, frame.Height, target, alphaMask);
    }

    internal static RgbImage CreateRgbImage(int width, int height, byte[] pixels, byte[]? alphaMask)
    {
        var pixelCount = checked(width * height);
        var requiredPixelBytes = checked(pixelCount * ColorChannelCount);
        if (pixels.Length < requiredPixelBytes)
        {
            throw new ArgumentException("The normalized image buffer is smaller than the declared image dimensions.", nameof(pixels));
        }

        if (alphaMask is not null && alphaMask.Length < checked(width * height))
        {
            throw new ArgumentException("The alpha mask is smaller than the normalized image.", nameof(alphaMask));
        }

        var effectivePixelCount = alphaMask is null ? pixelCount : CountActivePixels(alphaMask);
        return new RgbImage(width, height, pixels, checked(width * ColorChannelCount), alphaMask, effectivePixelCount);
    }

    internal static long GetRgbImageByteCount(RgbImage image) =>
        checked(image.Pixels.LongLength + (image.AlphaMask?.LongLength ?? 0));

    internal static int CountEffectivePixels(RgbImage image)
    {
        var pixelCount = checked(image.Width * image.Height);
        if (image.AlphaMask is null)
        {
            return pixelCount;
        }

        return image.EffectivePixelCount >= 0
            ? image.EffectivePixelCount
            : CountActivePixels(image.AlphaMask.AsSpan(0, pixelCount));
    }

    private static int CountActivePixels(ReadOnlySpan<byte> alphaMask)
    {
        var count = 0;
        foreach (var value in alphaMask)
        {
            if (value is not 0)
            {
                count++;
            }
        }

        return count;
    }

    internal static RgbImage NormalizePooledFrame(ScreenFrame frame, ScreenRect region, CancellationToken cancellationToken)
    {
        var requiredLength = checked(region.Width * region.Height * ColorChannelCount);
        var target = ArrayPool<byte>.Shared.Rent(requiredLength);
        try
        {
            NormalizeFrameInto(frame, target, alphaMask: null, preserveAlphaCoverage: false, region, alphaThreshold: 1, cancellationToken);
            return new RgbImage(region.Width, region.Height, target, checked(region.Width * ColorChannelCount));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            ArrayPool<byte>.Shared.Return(target);
            throw;
        }
    }

    private static void NormalizeFrameInto(
        ScreenFrame frame,
        byte[] target,
        byte[]? alphaMask,
        bool preserveAlphaCoverage,
        ScreenRect region,
        byte alphaThreshold,
        CancellationToken cancellationToken)
    {
        var bytesPerPixel = ScreenFrame.GetBytesPerPixel(frame.PixelFormat);
        var source = frame.Pixels.Span;
        var rowLength = checked(region.Width * ColorChannelCount);
        var sourceOriginX = checked(region.X - frame.LogicalBounds.X);
        var sourceOriginY = checked(region.Y - frame.LogicalBounds.Y);

        if (!ShouldParallelizeRows(region.Width, region.Height)
            || !MemoryMarshal.TryGetArray(frame.Pixels, out var sourceSegment)
            || sourceSegment.Array is not { } sourceArray)
        {
            for (var y = 0; y < region.Height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (var x = 0; x < region.Width; x++)
                {
                    var sourceOffset = checked(((sourceOriginY + y) * frame.Stride) + ((sourceOriginX + x) * bytesPerPixel));
                    var targetOffset = checked((y * rowLength) + (x * ColorChannelCount));
                    WriteNormalizedPixel(source, sourceOffset, frame.PixelFormat, frame.AlphaMode, target, targetOffset, alphaMask, preserveAlphaCoverage, (y * region.Width) + x, alphaThreshold);
                    ApplyValidityMask(frame, region, x, y, alphaMask);
                }
            }
        }
        else
        {
            void NormalizeRow(int y)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sourceRowOffset = checked(sourceSegment.Offset + ((sourceOriginY + y) * frame.Stride) + (sourceOriginX * bytesPerPixel));
                var targetRowOffset = checked(y * rowLength);
                for (var x = 0; x < region.Width; x++)
                {
                    var sourceOffset = checked(sourceRowOffset + (x * bytesPerPixel));
                    var targetOffset = checked(targetRowOffset + (x * ColorChannelCount));
                    WriteNormalizedPixel(sourceArray, sourceOffset, frame.PixelFormat, frame.AlphaMode, target, targetOffset, alphaMask, preserveAlphaCoverage, (y * region.Width) + x, alphaThreshold);
                    ApplyValidityMask(frame, region, x, y, alphaMask);
                }
            }

            _ = Parallel.For(
                0,
                region.Height,
                CreateParallelOptions(cancellationToken),
                NormalizeRow);
        }
    }

    private static void WriteNormalizedPixel(
        ReadOnlySpan<byte> source,
        int sourceOffset,
        ScreenPixelFormat pixelFormat,
        ScreenAlphaMode alphaMode,
        byte[] target,
        int targetOffset,
        byte[]? alphaMask,
        bool preserveAlphaCoverage,
        int alphaOffset,
        byte alphaThreshold)
    {
        byte red;
        byte green;
        byte blue;
        byte alpha = byte.MaxValue;
        switch (pixelFormat)
        {
            case ScreenPixelFormat.Rgb24:
            case ScreenPixelFormat.Xbgr8888:
            case ScreenPixelFormat.Abgr8888:
                red = source[sourceOffset];
                green = source[sourceOffset + 1];
                blue = source[sourceOffset + 2];
                if (pixelFormat is ScreenPixelFormat.Abgr8888)
                {
                    alpha = source[sourceOffset + 3];
                }
                break;
            case ScreenPixelFormat.Bgr24:
            case ScreenPixelFormat.Xrgb8888:
            case ScreenPixelFormat.Bgra8888:
                red = source[sourceOffset + 2];
                green = source[sourceOffset + 1];
                blue = source[sourceOffset];
                if (pixelFormat is ScreenPixelFormat.Bgra8888)
                {
                    alpha = source[sourceOffset + 3];
                }
                break;
            default:
                throw new InvalidOperationException($"Unsupported screen pixel format '{pixelFormat}'.");
        }

        if (alphaMode is ScreenAlphaMode.Premultiplied)
        {
            red = Unpremultiply(red, alpha);
            green = Unpremultiply(green, alpha);
            blue = Unpremultiply(blue, alpha);
        }

        target[targetOffset] = red;
        target[targetOffset + 1] = green;
        target[targetOffset + 2] = blue;
        if (alphaMask is { } mask)
        {
            if (!preserveAlphaCoverage)
            {
                mask[alphaOffset] = byte.MaxValue;
                return;
            }

            mask[alphaOffset] = alpha < alphaThreshold ? (byte)0 : alpha;
        }
    }

    private static void ApplyValidityMask(ScreenFrame frame, ScreenRect region, int localX, int localY, byte[]? alphaMask)
    {
        if (alphaMask is not null
            && !frame.IsFullyValid
            && !frame.IsPixelValid(new ScreenPoint(region.X + localX, region.Y + localY)))
        {
            alphaMask[(localY * region.Width) + localX] = 0;
        }
    }

    private static byte Unpremultiply(byte value, byte alpha) =>
        alpha is 0 ? (byte)0 : (byte)Math.Min(byte.MaxValue, ((value * 255) + (alpha / 2)) / alpha);
}
