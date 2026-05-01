using System;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace CrossMacro.UI.Services;

internal sealed class DesktopLifetimeContext : IDesktopLifetimeContext
{
    public IClassicDesktopStyleApplicationLifetime? DesktopLifetime { get; private set; }

    public Window? MainWindow { get; private set; }

    public void Attach(IClassicDesktopStyleApplicationLifetime desktopLifetime)
    {
        DesktopLifetime = desktopLifetime ?? throw new ArgumentNullException(nameof(desktopLifetime));
        MainWindow = desktopLifetime.MainWindow;
    }

    public void SetMainWindow(Window? mainWindow)
    {
        MainWindow = mainWindow;
        if (DesktopLifetime != null)
        {
            DesktopLifetime.MainWindow = mainWindow;
        }
    }
}
