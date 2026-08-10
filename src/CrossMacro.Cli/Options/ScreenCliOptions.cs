
namespace CrossMacro.Cli.Options;

public sealed record ScreenCliOptions(
    ScreenCliAction Action,
    int X = 0,
    int Y = 0,
    ScreenPixelColor? ExpectedColor = null,
    bool Relative = false,
    int? X2 = null,
    int? Y2 = null,
    int? TimeoutMs = null,
    int Tolerance = 0,
    string? ImagePath = null,
    int? RegionX = null,
    int? RegionY = null,
    int? RegionWidth = null,
    int? RegionHeight = null,
    double Similarity = 0.95,
    ScreenImageMatchMode MatchMode = ScreenImageMatchMode.Automatic,
    MacroMouseButton Button = MacroMouseButton.Left,
    bool JsonOutput = false,
    string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);
