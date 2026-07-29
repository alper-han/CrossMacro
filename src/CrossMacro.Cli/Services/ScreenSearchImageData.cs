namespace CrossMacro.Cli.Services;

public sealed record ScreenSearchImageData(
    bool Found,
    int? X,
    int? Y,
    double? Score,
    string ImagePath,
    int? RegionX,
    int? RegionY,
    int? RegionWidth,
    int? RegionHeight,
    double Similarity,
    int Downsample,
    string MatchMode,
    bool ScaleAware,
    string ProviderName);
