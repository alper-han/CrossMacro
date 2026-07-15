namespace CrossMacro.Cli.Services;

public sealed record class ScreenSearchColorData(
    bool Found,
    int? X,
    int? Y,
    string? Color,
    string ExpectedColor,
    int RegionX,
    int RegionY,
    int RegionWidth,
    int RegionHeight,
    int Tolerance,
    string ProviderName);
