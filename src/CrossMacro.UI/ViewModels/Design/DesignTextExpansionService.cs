
namespace CrossMacro.UI.ViewModels.Design;

internal sealed class DesignTextExpansionService : ITextExpansionService
{
    public bool IsRunning { get; private set; }

    public void Start() => IsRunning = true;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Start();
        return Task.CompletedTask;
    }

    public void StopExpansion() => IsRunning = false;

    public Task StopExpansionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StopExpansion();
        return Task.CompletedTask;
    }

    public void Dispose() { /* Empty */ }
}
