using static CrossMacro.Infrastructure.Services.ScreenReading.ImageMatching.AutomaticMatchPolicy;

namespace CrossMacro.Infrastructure.Services.ScreenReading.ImageMatching;

/// <summary>Best and runner-up automatic candidates; decides whether the evidence is strong and distinct enough to report a match.</summary>
internal readonly record struct MatchEvidence(AutomaticCandidate? Best, AutomaticCandidate? SecondBest, double Margin, double Coverage, double Scale, string? Profile)
{
    public static MatchEvidence Create(IEnumerable<AutomaticCandidate> candidates)
    {
        var ordered = candidates.Order(AutomaticCandidateComparer.Instance).ToArray();
        var best = ordered.FirstOrDefault();
        if (ordered.Length is 0)
        {
            return new MatchEvidence(Best: null, SecondBest: null, Margin: 0.0, Coverage: 0.0, Scale: 1.0, Profile: null);
        }

        AutomaticCandidate? second = null;
        foreach (var candidate in ordered)
        {
            if (IsSpatiallyDistinct(best, candidate))
            {
                second = candidate;
                break;
            }
        }

        return new MatchEvidence(best, second, second is { } runnerUp ? Math.Max(0.0, best.Score - runnerUp.Score) : best.Score, best.Coverage, best.Scale, best.Profile);
    }

    public bool IsExactNativeMatch(double minimumSimilarity, bool requireDistinctEvidence) => Best is { } best && best.Profile is "weighted-sad" && Math.Abs(best.Scale - 1.0) < ScaleScoreTieTolerance && best.Score >= 1.0 && IsAcceptable(minimumSimilarity, requireDistinctEvidence);

    public ScreenImageMatch? ToMatch(double minimumSimilarity, bool requireDistinctEvidence)
    {
        if (Best is not { } best || !IsAcceptable(minimumSimilarity, requireDistinctEvidence))
        {
            return null;
        }

        var isNativeScale = Math.Abs(best.Scale - 1.0) < ScaleScoreTieTolerance;
        return new ScreenImageMatch(new ScreenPoint(best.X, best.Y), best.Score, isNativeScale ? 0 : best.Width, isNativeScale ? 0 : best.Height);
    }

    private bool IsAcceptable(double minimumSimilarity, bool requireDistinctEvidence) => Best is { } best && best.Score >= minimumSimilarity && (best.Profile is not "luma-ncc" || best.EffectivePixels >= MinimumAutomaticAppearanceEffectivePixels) && (!requireDistinctEvidence || SecondBest is null || Margin >= AutomaticMinimumEvidenceMargin);

    private static bool IsSpatiallyDistinct(AutomaticCandidate best, AutomaticCandidate candidate)
    {
        if (candidate == best)
        {
            return false;
        }

        var bestCenterX = best.X + (best.Width / 2.0);
        var bestCenterY = best.Y + (best.Height / 2.0);
        var candidateCenterX = candidate.X + (candidate.Width / 2.0);
        var candidateCenterY = candidate.Y + (candidate.Height / 2.0);
        return Math.Abs(bestCenterX - candidateCenterX) > AutomaticSameTargetCenterTolerance || Math.Abs(bestCenterY - candidateCenterY) > AutomaticSameTargetCenterTolerance;
    }
}
