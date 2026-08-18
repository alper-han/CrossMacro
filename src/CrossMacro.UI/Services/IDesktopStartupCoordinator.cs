
namespace CrossMacro.UI.Services;

public interface IDesktopStartupCoordinator
{
    public Task StartAsync(IClassicDesktopStyleApplicationLifetime desktop);
}
