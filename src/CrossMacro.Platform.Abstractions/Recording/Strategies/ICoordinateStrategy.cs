
namespace CrossMacro.Platform.Abstractions;

public interface ICoordinateStrategy : IDisposable
{
    public bool ProducesLogicalCoordinates { get; }

    public bool ProducesRelativeCoordinates { get; }

    public Task InitializeAsync(CancellationToken ct);

    public CoordinateSample ProcessPosition(CapturedInputEvent e);
}
