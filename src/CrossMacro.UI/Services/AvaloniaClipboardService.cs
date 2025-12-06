using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Threading;
using CrossMacro.Core.Services;
using Serilog;

namespace CrossMacro.UI.Services;

public class AvaloniaClipboardService : IClipboardService
{
    public async Task SetTextAsync(string text)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            try
            {
                var clipboard = GetClipboard();
                if (clipboard != null)
                {
                    await clipboard.SetTextAsync(text);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to set clipboard text via Avalonia");
            }
        });
    }

    public async Task<string?> GetTextAsync()
    {
        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            try
            {
                var clipboard = GetClipboard();
                if (clipboard != null)
                {
#pragma warning disable CS0618 // Type or member is obsolete
                    return await clipboard.GetTextAsync();
#pragma warning restore CS0618 // Type or member is obsolete
                }
                return null;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to get clipboard text via Avalonia");
                return null;
            }
        });
    }

    private Avalonia.Input.Platform.IClipboard? GetClipboard()
    {
        // Try to get clipboard from the main window or active application lifetime
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow?.Clipboard;
        }
        
        // Fallback for other lifetimes if needed (e.g. single view)
        return TopLevel.GetTopLevel(null)?.Clipboard;
    }
}
