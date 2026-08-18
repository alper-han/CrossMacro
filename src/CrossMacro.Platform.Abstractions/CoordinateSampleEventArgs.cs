namespace CrossMacro.Platform.Abstractions;

public sealed class CoordinateSampleEventArgs(
    CoordinateSample sample,
    CoordinateSampleSpace coordinateSpace) : EventArgs
{
    public CoordinateSample Sample { get; } = sample;

    public CoordinateSampleSpace CoordinateSpace { get; } = coordinateSpace;
}
