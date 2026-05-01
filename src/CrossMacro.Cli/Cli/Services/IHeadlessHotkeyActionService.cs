using System;
using System.Threading;
using System.Threading.Tasks;

namespace CrossMacro.Cli.Services;

public interface IHeadlessHotkeyActionService : IDisposable, IAsyncDisposable
{
    bool IsRunning { get; }

    void Start();

    void Stop();

    Task StopAsync(CancellationToken cancellationToken = default);
}
