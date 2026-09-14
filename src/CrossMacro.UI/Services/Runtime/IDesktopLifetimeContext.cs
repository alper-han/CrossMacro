
namespace CrossMacro.UI.Services.Runtime;

public interface IDesktopLifetimeContext
{
    public IClassicDesktopStyleApplicationLifetime? DesktopLifetime { get; }

    public Window? MainWindow { get; }

    public void Attach(IClassicDesktopStyleApplicationLifetime desktopLifetime);

    public void SetMainWindow(Window? mainWindow);
}
