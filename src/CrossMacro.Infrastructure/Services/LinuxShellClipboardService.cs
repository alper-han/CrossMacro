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
    private enum ClipboardTool { Unknown, WlClipboard, Xclip, Xsel }
    private ClipboardTool _tool = ClipboardTool.Unknown;
    private bool _initialized = false;

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;

        // Check for Wayland first
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
        {
            if (await CheckCommandAsync("wl-copy")) 
            {
                _tool = ClipboardTool.WlClipboard;
                Log.Information("[LinuxClipboard] Detected Wayland, using wl-clipboard");
                _initialized = true;
                return;
            }
        }

        // Check for X11 tools
        if (await CheckCommandAsync("xclip"))
        {
            _tool = ClipboardTool.Xclip;
            Log.Information("[LinuxClipboard] Using xclip");
            _initialized = true;
            return;
        }

        if (await CheckCommandAsync("xsel"))
        {
            _tool = ClipboardTool.Xsel;
            Log.Information("[LinuxClipboard] Using xsel");
            _initialized = true;
            return;
        }

        Log.Warning("[LinuxClipboard] No supported clipboard tool found (wl-copy, xclip, xsel missing)");
        _initialized = true;
    }

    public async Task SetTextAsync(string text)
    {
        await EnsureInitializedAsync();

        try
        {
            switch (_tool)
            {
                case ClipboardTool.WlClipboard:
                    // --type text/plain helps some compositors
                    await RunCommandAsync("wl-copy", "--type text/plain", text);
                    break;
                case ClipboardTool.Xclip:
                    await RunCommandAsync("xclip", "-selection clipboard", text);
                    break;
                case ClipboardTool.Xsel:
                    await RunCommandAsync("xsel", "--clipboard --input", text);
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
        await EnsureInitializedAsync();

        try
        {
            return _tool switch
            {
                ClipboardTool.WlClipboard => await ReadCommandAsync("wl-paste", "--no-newline"),
                ClipboardTool.Xclip => await ReadCommandAsync("xclip", "-selection clipboard -o"),
                ClipboardTool.Xsel => await ReadCommandAsync("xsel", "--clipboard --output"),
                _ => null
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to get clipboard text via shell");
            return null;
        }
    }

    private static async Task<bool> CheckCommandAsync(string command)
    {
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "which",
                Arguments = command,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            
            if (proc == null)
            {
                return false;
            }
            
            await proc.WaitForExitAsync();
            return proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task RunCommandAsync(string command, string args, string input)
    {
        using var proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = args,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        
        proc.Start();
        await proc.StandardInput.WriteAsync(input);
        proc.StandardInput.Close(); 
        
        await proc.WaitForExitAsync();
    }

    private static async Task<string> ReadCommandAsync(string command, string args)
    {
        using var proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = args,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        
        proc.Start();
        var result = await proc.StandardOutput.ReadToEndAsync();
        await proc.WaitForExitAsync();
        return result;
    }
}
