using System.Runtime.InteropServices;

namespace CrossMacro.Infrastructure.Services.ScreenReading.ImageMatching;

/// <summary>A candidate position scored by its sum of absolute differences; lower is better.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct MatchCandidate(int X, int Y, long Sad, double MaximumSad = 0, int Width = 0, int Height = 0, double Scale = 1.0)
{
    public static MatchCandidate None => new(0, 0, long.MaxValue);

    public bool HasValue => Sad != long.MaxValue;

    public static MatchCandidate BetterOf(MatchCandidate current, MatchCandidate candidate)
    {
        if (!candidate.HasValue)
        {
            return current;
        }

        if (!current.HasValue || candidate.Sad < current.Sad || (candidate.Sad == current.Sad && (candidate.Y < current.Y || (candidate.Y == current.Y && candidate.X < current.X))))
        {
            return candidate;
        }

        return current;
    }

    public static MatchCandidate EarlierOf(MatchCandidate current, MatchCandidate candidate)
    {
        if (!candidate.HasValue)
        {
            return current;
        }

        if (!current.HasValue || candidate.Y < current.Y || (candidate.Y == current.Y && candidate.X < current.X))
        {
            return candidate;
        }

        return current;
    }
}
