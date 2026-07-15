
namespace CrossMacro.Infrastructure.Tests.Services.ScreenReading;

internal static class ScalarScreenImageMatcher
{
    public static ScreenImageMatch? FindMatch(
        ScreenFrame frame,
        ScreenFrame template,
        ScreenImageMatchOptions? options = null,
        ScalarMatchSelection selection = ScalarMatchSelection.BestMatch,
        CancellationToken cancellationToken = default)
    {
        options ??= ScreenImageMatchOptions.Default;
        var region = options.SearchRegion ?? frame.LogicalBounds;
        var sampleCount = GetSampleCount(template.Width, options.DownsampleFactor)
            * GetSampleCount(template.Height, options.DownsampleFactor);
        var allowedSad = sampleCount * 3 * 255.0 * (1.0 - options.MinimumSimilarity);
        var maximumSad = sampleCount * 3 * 255.0;
        ScreenImageMatch? best = null;
        long bestSad = long.MaxValue;

        for (var y = region.Y; y <= region.Bottom - template.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = region.X; x <= region.Right - template.Width; x++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsValid(frame, x, y, template.Width, template.Height))
                {
                    continue;
                }

                var sad = ComputeSad(frame, template, x, y, options.DownsampleFactor, cancellationToken);
                if (sad > allowedSad)
                {
                    continue;
                }

                var candidate = new ScreenImageMatch(new ScreenPoint(x, y), 1.0 - (sad / maximumSad));
                if (selection is ScalarMatchSelection.FirstThresholdMatch)
                {
                    return candidate;
                }

                if (best is null
                    || sad < bestSad
                    || (sad == bestSad
                        && (candidate.Point.Y < best.Value.Point.Y
                            || (candidate.Point.Y == best.Value.Point.Y && candidate.Point.X < best.Value.Point.X))))
                {
                    best = candidate;
                    bestSad = sad;
                }
            }
        }

        return best;
    }

    private static long ComputeSad(
        ScreenFrame frame,
        ScreenFrame template,
        int candidateX,
        int candidateY,
        int downsampleFactor,
        CancellationToken cancellationToken)
    {
        long sad = 0;
        for (var templateY = 0; templateY < template.Height; templateY += downsampleFactor)
        {
            for (var templateX = 0; templateX < template.Width; templateX += downsampleFactor)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var frameColor = frame.GetPixel(new ScreenPoint(candidateX + templateX, candidateY + templateY));
                var templateColor = template.GetPixel(new ScreenPoint(template.LogicalBounds.X + templateX, template.LogicalBounds.Y + templateY));
                sad += Math.Abs(frameColor.R - templateColor.R)
                    + Math.Abs(frameColor.G - templateColor.G)
                    + Math.Abs(frameColor.B - templateColor.B);
            }
        }

        return sad;
    }

    private static bool IsValid(ScreenFrame frame, int x, int y, int width, int height)
    {
        for (var row = 0; row < height; row++)
        {
            for (var column = 0; column < width; column++)
            {
                if (!frame.IsPixelValid(new ScreenPoint(x + column, y + row)))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static int GetSampleCount(int length, int downsampleFactor) => ((length - 1) / downsampleFactor) + 1;
}
