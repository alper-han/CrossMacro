namespace CrossMacro.Infrastructure.Services.ScreenReading.ImageMatching;

internal readonly record struct AutomaticSearchResult(List<AutomaticCandidate> Candidates, bool CoarseEvidenceSufficient);
