using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using CrossMacro.Core.Logging;
using CrossMacro.Core.Services;

namespace CrossMacro.UI.Services;

public class AvaloniaClipboardService : IClipboardService
{
    private readonly IDesktopLifetimeContext _desktopLifetimeContext;

    public AvaloniaClipboardService(IDesktopLifetimeContext desktopLifetimeContext)
    {
        _desktopLifetimeContext = desktopLifetimeContext;
    }

    public bool IsSupported => true; // Avalonia clipboard is generally supported if UI is running

    public async Task SetTextAsync(string text)
    {
        Log.Debug("[AvaloniaClipboard] SetTextAsync called for length {Length}", text.Length);

        if (_desktopLifetimeContext.MainWindow == null)
        {
            Log.Warning("[AvaloniaClipboard] SetTextAsync skipped because desktop main window is unavailable");
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            Log.Debug("[AvaloniaClipboard] SetTextAsync running on UI thread");
            var clipboard = GetClipboard();
            if (clipboard != null)
            {
                try 
                {
                    Log.Debug("[AvaloniaClipboard] Setting text to clipboard instance: {Type}", clipboard.GetType().Name);
                    await ClipboardExtensions.SetTextAsync(clipboard, text);
                    Log.Debug("[AvaloniaClipboard] SetTextAsync completed successfully");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[AvaloniaClipboard] Exception during SetTextAsync");
                }
            }
            else
            {
                Log.Warning("[AvaloniaClipboard] SetTextAsync: Clipboard is null");
            }
        });
    }

    public async Task<string?> GetTextAsync()
    {
        Log.Debug("[AvaloniaClipboard] GetTextAsync called");

        if (_desktopLifetimeContext.MainWindow == null)
        {
            Log.Warning("[AvaloniaClipboard] GetTextAsync skipped because desktop main window is unavailable");
            return null;
        }

        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            try
            {
                var clipboard = GetClipboard();
                if (clipboard != null)
                {
                    return await ClipboardExtensions.TryGetTextAsync(clipboard);
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

    private IClipboard? GetClipboard()
    {
        var mainWindow = _desktopLifetimeContext.MainWindow;
        if (mainWindow == null)
        {
            Log.Warning("[AvaloniaClipboard] Main window is unavailable. Clipboard access skipped.");
            return null;
        }

        var clipboard = mainWindow.Clipboard;
        if (clipboard != null)
        {
            return clipboard;
        }

        Log.Warning("[AvaloniaClipboard] Main window clipboard is unavailable.");
        return null;
    }
}
