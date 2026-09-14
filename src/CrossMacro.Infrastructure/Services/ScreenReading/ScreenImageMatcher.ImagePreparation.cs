namespace CrossMacro.Infrastructure.Services.ScreenReading;

public sealed partial class ScreenImageMatcher
{
    /// <summary>Pure image normalization, alpha-aware resampling and pyramid preparation.</summary>
    private static class ImagePreparation
    {
        public static List<RgbImage> BuildGaussianPyramid(
            RgbImage source,
            SearchBudget budget,
            CancellationToken cancellationToken,
            int maximumLevels = MaximumPyramidLevels)
        {
            var result = new List<RgbImage> { source };
            maximumLevels = Math.Clamp(maximumLevels, 1, MaximumPyramidLevels);
            while (result.Count < maximumLevels
                && result[^1].Width > MinimumPyramidTemplateExtent
                && result[^1].Height > MinimumPyramidTemplateExtent)
            {
                var current = result[^1];
                budget.ConsumePreparation(EstimateResamplingWork(current.Width, current.Height, sourcePixelCountPerOutput: 25));
                result.Add(GaussianDownsample(current, cancellationToken));
            }

            return result;
        }

        public static RgbImage GaussianDownsample(RgbImage source, CancellationToken cancellationToken)
        {
            ReadOnlySpan<int> kernel = [1, 4, 6, 4, 1];
            var width = Math.Max(1, (source.Width + 1) / 2);
            var height = Math.Max(1, (source.Height + 1) / 2);
            var pixels = new byte[checked(width * height * ColorChannelCount)];
            byte[]? coverage = source.AlphaMask is null ? null : new byte[checked(width * height)];
            for (var y = 0; y < height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (var x = 0; x < width; x++)
                {
                    long coverageTotal = 0;
                    long totalWeight = 0;
                    long red = 0;
                    long green = 0;
                    long blue = 0;
                    for (var ky = -2; ky <= 2; ky++)
                    {
                        var sourceY = Math.Clamp((y * 2) + ky, 0, source.Height - 1);
                        for (var kx = -2; kx <= 2; kx++)
                        {
                            var sourceX = Math.Clamp((x * 2) + kx, 0, source.Width - 1);
                            var filterWeight = kernel[ky + 2] * kernel[kx + 2];
                            var sourceCoverage = source.AlphaMask is null ? byte.MaxValue : source.AlphaMask[(sourceY * source.Width) + sourceX];
                            var effectiveWeight = filterWeight * (long)sourceCoverage;
                            var offset = (sourceY * source.RowStride) + (sourceX * ColorChannelCount);
                            totalWeight += filterWeight;
                            coverageTotal += effectiveWeight;
                            red += source.Pixels[offset] * effectiveWeight;
                            green += source.Pixels[offset + 1] * effectiveWeight;
                            blue += source.Pixels[offset + 2] * effectiveWeight;
                        }
                    }

                    var targetOffset = ((y * width) + x) * ColorChannelCount;
                    if (coverageTotal is not 0)
                    {
                        pixels[targetOffset] = (byte)((red + (coverageTotal / 2)) / coverageTotal);
                        pixels[targetOffset + 1] = (byte)((green + (coverageTotal / 2)) / coverageTotal);
                        pixels[targetOffset + 2] = (byte)((blue + (coverageTotal / 2)) / coverageTotal);
                    }

                    if (coverage is { })
                    {
                        coverage[(y * width) + x] = (byte)((coverageTotal + (totalWeight / 2)) / totalWeight);
                    }
                }
            }

            return CreateRgbImage(width, height, pixels, coverage);
        }

        public static RgbImage ResizeLinear(RgbImage source, int width, int height, CancellationToken cancellationToken)
        {
            var pixels = new byte[checked(width * height * ColorChannelCount)];
            byte[]? alphaMask = source.AlphaMask is null ? null : new byte[checked(width * height)];
            for (var y = 0; y < height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sourceYPosition = ((y + 0.5) * source.Height / height) - 0.5;
                var sourceYFloor = (int)Math.Floor(sourceYPosition);
                var sourceY = Math.Clamp(sourceYFloor, 0, source.Height - 1);
                var nextY = Math.Clamp(sourceYFloor + 1, 0, source.Height - 1);
                var yFraction = Math.Clamp(sourceYPosition - sourceYFloor, 0.0, 1.0);
                for (var x = 0; x < width; x++)
                {
                    var sourceXPosition = ((x + 0.5) * source.Width / width) - 0.5;
                    var sourceXFloor = (int)Math.Floor(sourceXPosition);
                    var sourceX = Math.Clamp(sourceXFloor, 0, source.Width - 1);
                    var nextX = Math.Clamp(sourceXFloor + 1, 0, source.Width - 1);
                    var xFraction = Math.Clamp(sourceXPosition - sourceXFloor, 0.0, 1.0);
                    var topLeft = checked(((sourceY * source.Width) + sourceX) * ColorChannelCount);
                    var topRight = checked(((sourceY * source.Width) + nextX) * ColorChannelCount);
                    var bottomLeft = checked(((nextY * source.Width) + sourceX) * ColorChannelCount);
                    var bottomRight = checked(((nextY * source.Width) + nextX) * ColorChannelCount);
                    var targetOffset = checked(((y * width) + x) * ColorChannelCount);
                    var topLeftCoverage = GetCoverage(source, sourceX, sourceY);
                    var topRightCoverage = GetCoverage(source, nextX, sourceY);
                    var bottomLeftCoverage = GetCoverage(source, sourceX, nextY);
                    var bottomRightCoverage = GetCoverage(source, nextX, nextY);
                    var coverage = Interpolate(topLeftCoverage, topRightCoverage, bottomLeftCoverage, bottomRightCoverage, xFraction, yFraction);
                    for (var channel = 0; channel < ColorChannelCount; channel++)
                    {
                        var top = (source.Pixels[topLeft + channel] * topLeftCoverage)
                            + (((source.Pixels[topRight + channel] * topRightCoverage) - (source.Pixels[topLeft + channel] * topLeftCoverage)) * xFraction);
                        var bottom = (source.Pixels[bottomLeft + channel] * bottomLeftCoverage)
                            + (((source.Pixels[bottomRight + channel] * bottomRightCoverage) - (source.Pixels[bottomLeft + channel] * bottomLeftCoverage)) * xFraction);
                        var premultiplied = top + ((bottom - top) * yFraction);
                        pixels[targetOffset + channel] = ToStraightColor(premultiplied, coverage);
                    }

                    SetCoverage(alphaMask, width, x, y, coverage);
                }
            }

            return CreateRgbImage(width, height, pixels, alphaMask);
        }

        public static RgbImage ResizeArea(RgbImage source, int width, int height, CancellationToken cancellationToken)
        {
            var pixels = new byte[checked(width * height * ColorChannelCount)];
            byte[]? alphaMask = source.AlphaMask is null ? null : new byte[checked(width * height)];
            var pixelArea = source.Width / (double)width * (source.Height / (double)height);
            var premultiplied = new double[ColorChannelCount];
            for (var y = 0; y < height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sourceTop = y * source.Height / (double)height;
                var sourceBottom = (y + 1) * source.Height / (double)height;
                var firstSourceY = Math.Max(0, (int)Math.Floor(sourceTop));
                var lastSourceY = Math.Min(source.Height - 1, (int)Math.Ceiling(sourceBottom) - 1);
                for (var x = 0; x < width; x++)
                {
                    var sourceLeft = x * source.Width / (double)width;
                    var sourceRight = (x + 1) * source.Width / (double)width;
                    var firstSourceX = Math.Max(0, (int)Math.Floor(sourceLeft));
                    var lastSourceX = Math.Min(source.Width - 1, (int)Math.Ceiling(sourceRight) - 1);
                    double coverageSum = 0;
                    Array.Clear(premultiplied);
                    for (var sourceY = firstSourceY; sourceY <= lastSourceY; sourceY++)
                    {
                        var yOverlap = Math.Min(sourceBottom, sourceY + 1.0) - Math.Max(sourceTop, sourceY);
                        for (var sourceX = firstSourceX; sourceX <= lastSourceX; sourceX++)
                        {
                            var overlap = yOverlap * (Math.Min(sourceRight, sourceX + 1.0) - Math.Max(sourceLeft, sourceX));
                            if (overlap <= 0.0)
                            {
                                continue;
                            }

                            var sourceCoverage = GetCoverage(source, sourceX, sourceY);
                            var weightedCoverage = overlap * sourceCoverage;
                            coverageSum += weightedCoverage;
                            var sourceOffset = checked(((sourceY * source.Width) + sourceX) * ColorChannelCount);
                            for (var channel = 0; channel < ColorChannelCount; channel++)
                            {
                                premultiplied[channel] += source.Pixels[sourceOffset + channel] * weightedCoverage;
                            }
                        }
                    }

                    var coverage = coverageSum / pixelArea;
                    var targetOffset = checked(((y * width) + x) * ColorChannelCount);
                    for (var channel = 0; channel < ColorChannelCount; channel++)
                    {
                        pixels[targetOffset + channel] = ToStraightColor(premultiplied[channel] / pixelArea, coverage);
                    }

                    SetCoverage(alphaMask, width, x, y, coverage);
                }
            }

            return CreateRgbImage(width, height, pixels, alphaMask);
        }

        public static double GetCoverage(RgbImage image, int x, int y) => image.AlphaMask is null
            ? byte.MaxValue
            : image.AlphaMask[(y * image.Width) + x];

        public static byte ToStraightColor(double premultiplied, double coverage) => coverage > 0.0
            ? (byte)Math.Clamp((int)Math.Round(premultiplied / coverage, MidpointRounding.AwayFromZero), 0, byte.MaxValue)
            : (byte)0;

        public static void SetCoverage(byte[]? alphaMask, int width, int x, int y, double coverage)
        {
            if (alphaMask is { })
            {
                alphaMask[(y * width) + x] = (byte)Math.Clamp(
                    (int)Math.Round(coverage, MidpointRounding.AwayFromZero),
                    0,
                    byte.MaxValue);
            }
        }

        public static double Interpolate(double topLeft, double topRight, double bottomLeft, double bottomRight, double xFraction, double yFraction)
        {
            var top = topLeft + ((topRight - topLeft) * xFraction);
            var bottom = bottomLeft + ((bottomRight - bottomLeft) * xFraction);
            return top + ((bottom - top) * yFraction);
        }

        public static RgbImage NormalizeFrame(ScreenFrame frame, bool useAlphaMask, byte alphaThreshold, CancellationToken cancellationToken)
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

        public static RgbImage CreateRgbImage(int width, int height, byte[] pixels, byte[]? alphaMask)
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

        public static long GetRgbImageByteCount(RgbImage image) =>
            checked(image.Pixels.LongLength + (image.AlphaMask?.LongLength ?? 0));

        public static int CountEffectivePixels(RgbImage image)
        {
            if (image.AlphaMask is null)
            {
                return checked(image.Width * image.Height);
            }

            var count = 0;
            for (var y = 0; y < image.Height; y++)
            {
                for (var x = 0; x < image.Width; x++)
                {
                    if (image.AlphaMask[(y * image.Width) + x] is not 0)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        public static int CountActivePixels(ReadOnlySpan<byte> alphaMask)
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

        public static RgbImage NormalizePooledFrame(ScreenFrame frame, ScreenRect region, CancellationToken cancellationToken)
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

        public static void NormalizeFrameInto(
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

            if (!MatchAlgorithms.ShouldParallelizeRows(region.Width, region.Height)
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
                    MatchAlgorithms.CreateParallelOptions(cancellationToken),
                    NormalizeRow);
            }
        }

        public static void WriteNormalizedPixel(
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
                red = MatchAlgorithms.Unpremultiply(red, alpha);
                green = MatchAlgorithms.Unpremultiply(green, alpha);
                blue = MatchAlgorithms.Unpremultiply(blue, alpha);
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

        public static void ApplyValidityMask(ScreenFrame frame, ScreenRect region, int localX, int localY, byte[]? alphaMask)
        {
            if (alphaMask is not null
                && !frame.IsFullyValid
                && !frame.IsPixelValid(new ScreenPoint(region.X + localX, region.Y + localY)))
            {
                alphaMask[(localY * region.Width) + localX] = 0;
            }
        }
    }
}
