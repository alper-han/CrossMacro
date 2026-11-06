using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

namespace CrossMacro.UI.Services.Desktop;

internal sealed class AvaloniaTrayIconBackend : ITrayIconBackend
{
    public ITrayIconHandle Create(WindowIcon icon, NativeMenu menu, string toolTipText)
    {
        var trayIcon = new TrayIcon
        {
            Icon = icon,
            Menu = menu,
            ToolTipText = toolTipText,
        };

        var application = Avalonia.Application.Current
            ?? throw new InvalidOperationException("Tray initialization requires the running application.");
        var trayIcons = TrayIcon.GetIcons(application);
        if (trayIcons is null)
        {
            trayIcons = new TrayIcons();
            TrayIcon.SetIcons(application, trayIcons);
        }
        trayIcons.Add(trayIcon);
        return new AvaloniaTrayIconHandle(trayIcon, trayIcons);
    }

    private sealed class AvaloniaTrayIconHandle(TrayIcon icon, TrayIcons icons) : ITrayIconHandle
    {
        public event EventHandler? Clicked
        {
            add => icon.Clicked += value;
            remove => icon.Clicked -= value;
        }

        public event EventHandler? AvailabilityChanged
        {
            add
            {
                // Avalonia exposes no registration-availability notifications to subscribe to.
            }
            remove
            {
                // No handler is retained because this backend cannot publish availability changes.
            }
        }

        public bool IsVisible
        {
            get => icon.IsVisible;
            set => icon.IsVisible = value;
        }

        public bool HasNativeMenuExporter => icon.NativeMenuExporter is not null;
        public bool IsReady => HasNativeMenuExporter;
        public Task WaitForReadyAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public string? GetToolTipText() => icon.ToolTipText;
        public void SetToolTipText(string? value) => icon.ToolTipText = value;

        public void Dispose()
        {
            _ = icons.Remove(icon);
            icon.Dispose();
        }
    }
}
