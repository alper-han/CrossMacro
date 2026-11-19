using static CrossMacro.Infrastructure.Services.ScreenReading.ImageMatching.MatcherWork;
using static CrossMacro.Infrastructure.Services.ScreenReading.ImageMatching.AutomaticMatchPolicy;

namespace CrossMacro.Infrastructure.Services.ScreenReading.ImageMatching;

/// <summary>Gaussian pyramids and alpha-aware resampling of normalized images.</summary>
internal static class ImageResampling
{
    internal static void ExtendGaussianPyramid(List<RgbImage> levels, int requiredLevels, SearchBudget budget, CancellationToken cancellationToken)
    {
        requiredLevels = Math.Clamp(requiredLevels, 1, MaximumPyramidLevels);
        while (levels.Count < requiredLevels
            && levels[^1].Width > MinimumPyramidTemplateExtent
            && levels[^1].Height > MinimumPyramidTemplateExtent)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = levels[^1];
            budget.ConsumePreparation(EstimateResamplingWork(current.Width, current.Height, sourcePixelCountPerOutput: 25));
            levels.Add(GaussianDownsample(current, cancellationToken));
        }
    }

    internal static List<TemplatePyramidLevel> BuildTemplatePyramid(PreparedTemplate prepared, SearchBudget budget, CancellationToken cancellationToken)
    {
        var images = new List<RgbImage> { prepared.Image };
        ExtendGaussianPyramid(images, MaximumPyramidLevels, budget, cancellationToken);
        var levels = new List<TemplatePyramidLevel>(images.Count) { new(prepared.Image, prepared.Statistics) };
        for (var i = 1; i < images.Count; i++)
        {
            levels.Add(new TemplatePyramidLevel(images[i], TemplateStatistics.Create(images[i])));
        }

        return levels;
    }

    private static RgbImage GaussianDownsample(RgbImage source, CancellationToken cancellationToken)
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

        return FrameNormalization.CreateRgbImage(width, height, pixels, coverage);
    }

    internal static RgbImage ResizeLinear(RgbImage source, int width, int height, CancellationToken cancellationToken)
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

        return FrameNormalization.CreateRgbImage(width, height, pixels, alphaMask);
    }

    internal static RgbImage ResizeArea(RgbImage source, int width, int height, CancellationToken cancellationToken)
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

        return FrameNormalization.CreateRgbImage(width, height, pixels, alphaMask);
    }

    private static double GetCoverage(RgbImage image, int x, int y) => image.AlphaMask is null
        ? byte.MaxValue
        : image.AlphaMask[(y * image.Width) + x];

    private static byte ToStraightColor(double premultiplied, double coverage) => coverage > 0.0
        ? (byte)Math.Clamp((int)Math.Round(premultiplied / coverage, MidpointRounding.AwayFromZero), 0, byte.MaxValue)
        : (byte)0;

    private static void SetCoverage(byte[]? alphaMask, int width, int x, int y, double coverage)
    {
        if (alphaMask is { })
        {
            alphaMask[(y * width) + x] = (byte)Math.Clamp(
                (int)Math.Round(coverage, MidpointRounding.AwayFromZero),
                0,
                byte.MaxValue);
        }
    }

    private static double Interpolate(double topLeft, double topRight, double bottomLeft, double bottomRight, double xFraction, double yFraction)
    {
        var top = topLeft + ((topRight - topLeft) * xFraction);
        var bottom = bottomLeft + ((bottomRight - bottomLeft) * xFraction);
        return top + ((bottom - top) * yFraction);
    }
}
