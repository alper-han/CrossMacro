namespace CrossMacro.Platform.Abstractions.Input.Pointer;

public interface ICoordinateSampleSource
{
    public event EventHandler<CoordinateSampleEventArgs>? SampleAvailable;
}
