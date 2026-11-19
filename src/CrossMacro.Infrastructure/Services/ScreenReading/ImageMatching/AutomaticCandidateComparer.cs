namespace CrossMacro.Infrastructure.Services.ScreenReading.ImageMatching;

internal sealed class AutomaticCandidateComparer : IComparer<AutomaticCandidate>
{
    public static AutomaticCandidateComparer Instance { get; } = new();

    public int Compare(AutomaticCandidate left, AutomaticCandidate right)
    {
        var score = right.Score.CompareTo(left.Score);
        if (score is not 0)
        {
            return score;
        }

        // Prefer RGB verification over equal correlation evidence.
        var profile = GetProfilePriority(left.Profile).CompareTo(GetProfilePriority(right.Profile));
        if (profile is not 0)
        {
            return profile;
        }

        // Prefer the least-rescaled result on equal scores.
        var scale = Math.Abs(left.Scale - 1.0).CompareTo(Math.Abs(right.Scale - 1.0));
        if (scale is not 0)
        {
            return scale;
        }

        var y = left.Y.CompareTo(right.Y);
        return y is not 0 ? y : left.X.CompareTo(right.X);
    }

    private static int GetProfilePriority(string profile) => profile switch
    {
        "weighted-sad" => 0,
        "luma-ncc" => 1,
        _ => 2,
    };
}
