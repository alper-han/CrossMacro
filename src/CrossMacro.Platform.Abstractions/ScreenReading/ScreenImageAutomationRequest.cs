
namespace CrossMacro.Platform.Abstractions.ScreenReading;

public sealed record ScreenImageAutomationRequest(
    string ImagePath,
    ScreenRect? Region = null,
    double Similarity = 0.95,
    ScreenImageMatchMode MatchMode = ScreenImageMatchMode.Automatic,
    TimeSpan? Timeout = null);
