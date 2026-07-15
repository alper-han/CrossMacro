
namespace CrossMacro.UI.ViewModels;

internal sealed class DesignTextExpansionService : ITextExpansionService
{
    public bool IsRunning { get; private set; }

    public void Start() => IsRunning = true;

    public void Stop() => IsRunning = false;

    public void Dispose()
    {
    }
}
