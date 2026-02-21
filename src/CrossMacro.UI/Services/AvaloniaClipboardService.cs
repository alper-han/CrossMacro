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
    public bool IsSupported => true; // Avalonia clipboard is generally supported if UI is running

    public async Task SetTextAsync(string text)
    {
        Log.Debug("[AvaloniaClipboard] SetTextAsync called for length {Length}", text.Length);

        var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        if (desktop?.MainWindow == null)
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
                    await clipboard.SetTextAsync(text);
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

        var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        if (desktop?.MainWindow == null)
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
#pragma warning disable CS0618 
                    return await clipboard.GetTextAsync();
#pragma warning restore CS0618 
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
        if (Application.Current == null)
        {
             Log.Error("[AvaloniaClipboard] Application.Current is null!");
             return null;
        }

        if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.MainWindow != null)
            {
                 var clipboard = desktop.MainWindow.Clipboard;
                 if (clipboard != null) return clipboard;
                 Log.Warning("[AvaloniaClipboard] desktop.MainWindow.Clipboard is null.");
            }
            else
            {
                 Log.Warning("[AvaloniaClipboard] desktop.MainWindow is null (Window might be closed/hidden).");
            }
        }
        else
        {
             Log.Warning("[AvaloniaClipboard] ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime.");
        }
        
        try 
        {
             var topLevel = TopLevel.GetTopLevel(null); 
             if (topLevel != null)
             {
                 if (topLevel.Clipboard != null) return topLevel.Clipboard;
                 Log.Warning("[AvaloniaClipboard] TopLevel found but Clipboard is null.");
             }
             else
             {
                 Log.Warning("[AvaloniaClipboard] TopLevel.GetTopLevel(null) returned null. No active visual root?");
             }
        }
        catch (Exception ex)
        {
             Log.Warning(ex, "[AvaloniaClipboard] Failed to look up TopLevel.");
        }
        
        if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLoop)
        {
             foreach (var window in desktopLoop.Windows)
             {
                 if (window.Clipboard != null)
                 {
                      Log.Information("[AvaloniaClipboard] Found clipboard via auxiliary window: {Title}", window.Title);
                      return window.Clipboard;
                 }
             }
        }

        Log.Error("[AvaloniaClipboard] Could not resolve any Clipboard instance. Avalonia clipboard unavailable.");
        return null;
    }
}
