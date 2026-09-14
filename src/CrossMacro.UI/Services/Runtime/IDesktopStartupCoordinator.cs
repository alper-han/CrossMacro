
namespace CrossMacro.UI.Services.Runtime;

public interface IDesktopStartupCoordinator
{
    public Task StartAsync(IClassicDesktopStyleApplicationLifetime desktop);
}
