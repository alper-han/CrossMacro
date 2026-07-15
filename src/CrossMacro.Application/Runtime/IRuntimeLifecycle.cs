
namespace CrossMacro.Application.Runtime;

public interface IRuntimeLifecycle : IAsyncDisposable
{
    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);
}
