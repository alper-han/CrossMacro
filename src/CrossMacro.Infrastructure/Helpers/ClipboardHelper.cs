using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Serilog;

namespace CrossMacro.Infrastructure.Helpers;

/// <summary>
/// Helper to interact with Linux clipboard via command line tools (wl-copy/paste, xclip, xsel)
/// </summary>
public class ClipboardHelper
{
    private enum ClipboardTool { Unknown, WlClipboard, Xclip, Xsel }
    private static ClipboardTool _tool = ClipboardTool.Unknown;

    private static async Task DetectToolAsync()
    {
        if (_tool != ClipboardTool.Unknown) return;

        // Check for Wayland first
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
        {
            if (await CheckCommandAsync("wl-copy")) 
            {
                _tool = ClipboardTool.WlClipboard;
                Log.Information("[ClipboardHelper] Detected Wayland, using wl-clipboard");
                return;
            }
        }

        // Check for X11 tools
        if (await CheckCommandAsync("xclip"))
        {
            _tool = ClipboardTool.Xclip;
            Log.Information("[ClipboardHelper] Using xclip");
            return;
        }

        if (await CheckCommandAsync("xsel"))
        {
            _tool = ClipboardTool.Xsel;
            Log.Information("[ClipboardHelper] Using xsel");
            return;
        }

        Log.Warning("[ClipboardHelper] No supported clipboard tool found (wl-copy, xclip, xsel missing)");
    }

    public static async Task SetTextAsync(string text)
    {
        await DetectToolAsync();

        try
        {
            switch (_tool)
            {
                case ClipboardTool.WlClipboard:
                    await RunCommandAsync("wl-copy", "", text);
                    break;
                case ClipboardTool.Xclip:
                    await RunCommandAsync("xclip", "-selection clipboard", text);
                    break;
                case ClipboardTool.Xsel:
                    await RunCommandAsync("xsel", "--clipboard --input", text);
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to set clipboard text");
        }
    }

    public static async Task<string> GetTextAsync()
    {
        await DetectToolAsync();

        try
        {
            return _tool switch
            {
                ClipboardTool.WlClipboard => await ReadCommandAsync("wl-paste", "--no-newline"),
                ClipboardTool.Xclip => await ReadCommandAsync("xclip", "-selection clipboard -o"),
                ClipboardTool.Xsel => await ReadCommandAsync("xsel", "--clipboard --output"),
                _ => string.Empty
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to get clipboard text");
            return string.Empty;
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
        
        // IMPORTANT: Close StandardInput to signal EOF, otherwise some tools wait forever
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
