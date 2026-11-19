
namespace CrossMacro.Cli.Headless;

public interface IHeadlessHotkeyActionService : IDisposable, IAsyncDisposable
{
    public bool IsRunning { get; }

    public void Start();

    public Task StopAsync(CancellationToken cancellationToken = default);
}
