
namespace CrossMacro.UI.Services;

public interface IDesktopStartupCoordinator
{
    Task StartAsync(IClassicDesktopStyleApplicationLifetime desktop);
}
