using System;
using System.Threading;
using System.Threading.Tasks;

namespace CrossMacro.Application.Runtime;

public interface IRuntimeLifecycle : IAsyncDisposable
{
    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);
}
