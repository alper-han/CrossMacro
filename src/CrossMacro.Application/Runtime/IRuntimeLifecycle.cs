
namespace CrossMacro.Application.Runtime;

public interface IRuntimeLifecycle : IAsyncDisposable
{
    public Task StartAsync(CancellationToken cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken);
}
