
namespace CrossMacro.Platform.Abstractions;

public sealed record class ScreenImageAutomationRequest(
    string ImagePath,
    ScreenRect? Region = null,
    double Similarity = 1.0,
    int Downsample = 1,
    ScreenImageMatchMode MatchMode = ScreenImageMatchMode.First,
    bool ScaleAware = false,
    TimeSpan? Timeout = null);
