using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Infrastructure.Services.ScreenReading;

public readonly record struct ScreenImageMatch(ScreenPoint Point, double Score, int MatchedWidth = 0, int MatchedHeight = 0);
