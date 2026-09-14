namespace CrossMacro.Platform.Abstractions.Input.Pointer;

public sealed class CoordinateSampleEventArgs(
    CoordinateSample sample,
    CoordinateSampleSpace coordinateSpace) : EventArgs
{
    public CoordinateSample Sample { get; } = sample;

    public CoordinateSampleSpace CoordinateSpace { get; } = coordinateSpace;
}
