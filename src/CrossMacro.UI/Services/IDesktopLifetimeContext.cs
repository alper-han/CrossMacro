using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace CrossMacro.UI.Services;

public interface IDesktopLifetimeContext
{
    IClassicDesktopStyleApplicationLifetime? DesktopLifetime { get; }

    Window? MainWindow { get; }

    void Attach(IClassicDesktopStyleApplicationLifetime desktopLifetime);

    void SetMainWindow(Window? mainWindow);
}
