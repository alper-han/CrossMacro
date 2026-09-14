
namespace CrossMacro.Platform.Abstractions.ScreenReading;

public sealed record ScreenImageAutomationRequest(
    string ImagePath,
    ScreenRect? Region = null,
    double Similarity = ScreenImageMatchDefaults.Similarity,
    ScreenImageMatchMode MatchMode = ScreenImageMatchDefaults.Mode,
    TimeSpan? Timeout = null);
