using static CrossMacro.Infrastructure.Services.ScreenReading.ImageMatching.MatcherWork;

namespace CrossMacro.Infrastructure.Services.ScreenReading.ImageMatching;

/// <summary>Luma and similarity scoring shared by the scanners.</summary>
internal static class MatchScoring
{
    internal static double GetLuma(RgbImage image, int x, int y)
    {
        var offset = (y * image.RowStride) + (x * ColorChannelCount);
        return ((77.0 * image.Pixels[offset]) + (150.0 * image.Pixels[offset + 1]) + (29.0 * image.Pixels[offset + 2])) / 256.0;
    }

    internal static long CalculateAllowedSad(double maximumSad, double minimumSimilarity)
    {
        return checked((long)Math.Floor(maximumSad * (1.0 - minimumSimilarity)));
    }

    internal static double CalculateScore(long sad, double maximumSad)
    {
        if (maximumSad <= 0.0)
        {
            return 1.0;
        }

        return Math.Clamp(1.0 - (sad / maximumSad), 0.0, 1.0);
    }
}
