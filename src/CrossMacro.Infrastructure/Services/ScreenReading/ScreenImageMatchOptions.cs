
namespace CrossMacro.Infrastructure.Services.ScreenReading;

public sealed record class ScreenImageMatchOptions
{
    public static ScreenImageMatchOptions Default { get; } = new();

    public static ScreenImageMatchOptions Create(
        ScreenRect? searchRegion,
        double minimumSimilarity,
        int downsampleFactor,
        ScreenImageMatchSelectionMode selectionMode = ScreenImageMatchSelectionMode.FirstThresholdMatch,
        bool scaleAware = false) => new()
        {
            SearchRegion = searchRegion,
            MinimumSimilarity = minimumSimilarity,
            DownsampleFactor = downsampleFactor,
            SelectionMode = selectionMode,
            ScaleAware = scaleAware,
        };

    public ScreenRect? SearchRegion { get; init; }

    public double MinimumSimilarity { get; init; } = 1.0;

    public int DownsampleFactor { get; init; } = 1;

    public ScreenImageMatchSelectionMode SelectionMode { get; init; } = ScreenImageMatchSelectionMode.FirstThresholdMatch;

    public bool ScaleAware { get; init; }

    public int AnchorPointCount { get; init; } = 8;
}
