
namespace CrossMacro.UI.Services.Runtime;

public interface IDesktopStartupCoordinator
{
    public Task StartAsync(IClassicDesktopStyleApplicationLifetime desktop);

    public Task StartAsync(IClassicDesktopStyleApplicationLifetime desktop, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return StartAsync(desktop);
    }
}
