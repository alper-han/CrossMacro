
namespace CrossMacro.Infrastructure.Services.ScreenReading;

public sealed record ScreenImageMatchOptions
{
    public static ScreenImageMatchOptions Default { get; } = new();

    public static ScreenImageMatchOptions Create(
        ScreenRect? searchRegion,
        double minimumSimilarity,
        ScreenImageMatchSelectionMode selectionMode = ScreenImageMatchSelectionMode.Automatic) => new()
        {
            SearchRegion = searchRegion,
            MinimumSimilarity = minimumSimilarity,
            SelectionMode = selectionMode,
        };

    public ScreenRect? SearchRegion { get; init; }

    public double MinimumSimilarity { get; init; } = 0.95;

    public ScreenImageMatchSelectionMode SelectionMode { get; init; } = ScreenImageMatchSelectionMode.Automatic;

    public int AnchorPointCount { get; init; } = 8;

    public byte AlphaThreshold { get; init; } = 1;

    public bool UseTemplateAlphaMask { get; init; } = true;
}
