using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CrossMacro.Core.Services;
using Serilog;

namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Clipboard service that uses Linux command line tools (wl-copy, xclip) 
/// to ensure reliable background operation where GUI frameworks fail.
/// </summary>
public class LinuxShellClipboardService : IClipboardService
{
    private readonly IProcessRunner _processRunner;
    private enum ClipboardTool { Unknown, WlClipboard, Xclip, Xsel, KdeKlipper }
    private ClipboardTool _tool = ClipboardTool.Unknown;
    private bool _initialized = false;

    public bool IsSupported => _tool != ClipboardTool.Unknown;

    public LinuxShellClipboardService(IProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        // Check for Wayland first
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
        {
            if (await _processRunner.CheckCommandAsync("wl-copy")) 
            {
                _tool = ClipboardTool.WlClipboard;
                Log.Information("[LinuxClipboard] Detected Wayland, using wl-clipboard");
                _initialized = true;
                return;
            }
        }

        // Check for X11 tools
        if (await _processRunner.CheckCommandAsync("xclip"))
        {
            _tool = ClipboardTool.Xclip;
            Log.Information("[LinuxClipboard] Using xclip");
            _initialized = true;
            return;
        }

        if (await _processRunner.CheckCommandAsync("xsel"))
        {
            _tool = ClipboardTool.Xsel;
            Log.Information("[LinuxClipboard] Using xsel");
            _initialized = true;
            return;
        }

        // Check for KDE Klipper (qdbus)
        if (await _processRunner.CheckCommandAsync("qdbus") && await CheckKlipperAsync())
        {
            _tool = ClipboardTool.KdeKlipper;
            Log.Information("[LinuxClipboard] Using KDE Klipper (qdbus)");
            _initialized = true;
            return;
        }

        Log.Warning("[LinuxClipboard] No supported clipboard tool found (wl-copy, xclip, xsel, qdbus+klipper missing)");
        _initialized = true;
    }

    public async Task SetTextAsync(string text)
    {
        await InitializeAsync();

        try
        {
            switch (_tool)
            {
                case ClipboardTool.WlClipboard:
                    await _processRunner.RunCommandAsync("wl-copy", "--type text/plain", text);
                    break;
                case ClipboardTool.Xclip:
                    await _processRunner.RunCommandAsync("xclip", "-selection clipboard", text);
                    break;
                case ClipboardTool.Xsel:
                    await _processRunner.RunCommandAsync("xsel", "--clipboard --input", text);
                    break;
                case ClipboardTool.KdeKlipper:
                    await RunQdbusSetAsync(text);
                    break;
                default:
                    Log.Warning("Cannot set clipboard: No tool available");
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to set clipboard text via shell");
        }
    }

    public async Task<string?> GetTextAsync()
    {
        await InitializeAsync();

        try
        {
            return _tool switch
            {
                ClipboardTool.WlClipboard => await _processRunner.ReadCommandAsync("wl-paste", "--no-newline"),
                ClipboardTool.Xclip => await _processRunner.ReadCommandAsync("xclip", "-selection clipboard -o"),
                ClipboardTool.Xsel => await _processRunner.ReadCommandAsync("xsel", "--clipboard --output"),
                ClipboardTool.KdeKlipper => await _processRunner.ReadCommandAsync("qdbus", "org.kde.klipper /klipper getClipboardContents"),
                _ => null
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to get clipboard text via shell");
            return null;
        }
    }

    // Helper to verify if Klipper service is available via qdbus
    private async Task<bool> CheckKlipperAsync()
    {
        try
        {

             var output = await _processRunner.ReadCommandAsync("qdbus", "org.kde.klipper");
             return !string.IsNullOrEmpty(output);
        }
        catch
        {
            return false;
        }
    }

    private async Task RunQdbusSetAsync(string text)
    {
        await _processRunner.ExecuteCommandAsync("qdbus", new[] 
        { 
            "org.kde.klipper", 
            "/klipper", 
            "setClipboardContents", 
            text 
        });
    }
}
