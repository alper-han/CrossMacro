
namespace CrossMacro.Platform.Abstractions;

public interface ICoordinateStrategy : IDisposable
{
    public Task InitializeAsync(CancellationToken ct);

    public (int X, int Y) ProcessPosition(CapturedInputEvent e);
}
