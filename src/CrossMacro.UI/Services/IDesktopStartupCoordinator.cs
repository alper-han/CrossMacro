using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;

namespace CrossMacro.UI.Services;

public interface IDesktopStartupCoordinator
{
    Task StartAsync(IClassicDesktopStyleApplicationLifetime desktop);
}
