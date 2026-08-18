namespace CrossMacro.Cli.Services;

public sealed record ScreenImageClickData(
    int X,
    int Y,
    double Score,
    string ImagePath,
    int? RegionX,
    int? RegionY,
    int? RegionWidth,
    int? RegionHeight,
    double Similarity,
    string MatchMode,
    string Button,
    string ProviderName);
