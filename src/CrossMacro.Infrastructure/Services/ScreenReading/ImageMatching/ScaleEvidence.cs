namespace CrossMacro.Infrastructure.Services.ScreenReading.ImageMatching;

internal readonly record struct ScaleEvidence(double Scale, IReadOnlyList<AutomaticCandidate> Candidates)
{
    public double BestScore => Candidates.Count is 0 ? double.NegativeInfinity : Candidates.Max(static candidate => candidate.Score);
}
