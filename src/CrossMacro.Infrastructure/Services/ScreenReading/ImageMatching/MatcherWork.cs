namespace CrossMacro.Infrastructure.Services.ScreenReading.ImageMatching;

/// <summary>Work budgets, work estimation and the parallelism policy shared by the matching algorithms.</summary>
internal static class MatcherWork
{
    internal const int ColorChannelCount = 3;
    internal const int MaxChannelDifference = byte.MaxValue;
    internal const long MaxMatcherWork = 100_000_000;
    // Bound setup separately from candidate comparisons.
    internal const long MaxMatcherPreparationWork = 1_000_000_000;
    private const long ParallelPixelThreshold = 256_000;
    private const int MinimumParallelRowWidth = 256;
    private const int MinimumParallelRowCount = 4;

    internal static long EstimateCandidateWork(RgbImage template, int anchorCount)
    {
        var pixelCount = SaturatingMultiply(template.Width, template.Height);
        return SaturatingMultiply(SaturatingAdd(pixelCount, Math.Max(0, anchorCount)), ColorChannelCount);
    }

    internal static long EstimatePixelWork(int width, int height, bool includeAlpha = false)
    {
        var channels = includeAlpha ? ColorChannelCount + 1 : ColorChannelCount;
        return SaturatingMultiply(SaturatingMultiply(width, height), channels);
    }

    internal static long EstimateResamplingWork(int width, int height, int sourcePixelCountPerOutput = 1)
    {
        var outputWork = EstimatePixelWork(width, height);
        return SaturatingMultiply(outputWork, sourcePixelCountPerOutput + 1L);
    }

    internal static long EstimateAutomaticScanWork(long candidateCount, RgbImage template, bool correlation)
    {
        var channels = correlation ? 9L : ColorChannelCount;
        return SaturatingMultiply(SaturatingMultiply(candidateCount, FrameNormalization.CountEffectivePixels(template)), channels);
    }

    internal static long SaturatingMultiply(long left, long right)
    {
        if (left <= 0 || right <= 0)
        {
            return 0;
        }

        return left > long.MaxValue / right ? long.MaxValue : left * right;
    }

    internal static long SaturatingAdd(long left, long right)
    {
        if (left <= 0)
        {
            return Math.Max(0, right);
        }

        if (right <= 0)
        {
            return left;
        }

        return left > long.MaxValue - right ? long.MaxValue : left + right;
    }

    internal static bool ShouldParallelizeRows(int width, int height) =>
        width >= MinimumParallelRowWidth
        && height >= MinimumParallelRowCount
        && (long)width * height >= ParallelPixelThreshold;

    internal static ParallelOptions CreateParallelOptions(CancellationToken cancellationToken) => new()
    {
        CancellationToken = cancellationToken,
        MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount),
    };
}
