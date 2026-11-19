using CrossMacro.Core.Models.Macros;

namespace CrossMacro.Commands.Options;

public sealed record ScreenOptions(
    ScreenAction Action,
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
    double Similarity = ScreenImageMatchDefaults.Similarity,
    ScreenImageMatchMode MatchMode = ScreenImageMatchDefaults.Mode,
    MacroMouseButton Button = MacroMouseButton.Left,
    bool JsonOutput = false,
    string? LogLevel = null)
    : CommandOptions(JsonOutput, LogLevel);
