namespace CrossMacro.Platform.Abstractions;

public sealed class CoordinateSampleEventArgs(CoordinateSample sample) : EventArgs
{
    public CoordinateSample Sample { get; } = sample;
}
