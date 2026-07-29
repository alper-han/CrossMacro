
namespace CrossMacro.Cli.Services;

public interface IHeadlessHotkeyActionService : IDisposable, IAsyncDisposable
{
    public bool IsRunning { get; }

    public void Start();

    public void StopHeadlessHotkeyActions();

    public Task StopAsync(CancellationToken cancellationToken = default);
}
