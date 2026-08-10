
namespace CrossMacro.Platform.Abstractions;

public sealed record ScreenImageAutomationRequest(
    string ImagePath,
    ScreenRect? Region = null,
    double Similarity = 0.95,
    ScreenImageMatchMode MatchMode = ScreenImageMatchMode.Automatic,
    TimeSpan? Timeout = null);
