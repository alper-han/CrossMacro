namespace CrossMacro.Platform.Abstractions;

public interface ICoordinateSampleSource
{
    public event EventHandler<CoordinateSampleEventArgs>? SampleAvailable;
}
