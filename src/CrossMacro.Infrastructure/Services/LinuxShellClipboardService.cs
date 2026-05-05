using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Logging;
using CrossMacro.Core.Services;

namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Clipboard service that uses Linux command line tools (wl-copy, xclip) 
/// to ensure reliable background operation where GUI frameworks fail.
/// </summary>
public class LinuxShellClipboardService : IClipboardService
{
    private readonly IProcessRunner _processRunner;
    private enum ClipboardTool { Unknown, WlClipboard, Xclip, Xsel }
    private ClipboardTool _tool = ClipboardTool.Unknown;
    private bool _initialized = false;

    public bool IsSupported => _tool != ClipboardTool.Unknown;

    public LinuxShellClipboardService(IProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized) return;

        // Check for Wayland first
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
        {
            if (await _processRunner.CheckCommandAsync("wl-copy", cancellationToken) &&
                await _processRunner.CheckCommandAsync("wl-paste", cancellationToken))
            {
                _tool = ClipboardTool.WlClipboard;
                Log.Information("[LinuxClipboard] Detected Wayland, using wl-clipboard");
                _initialized = true;
                return;
            }
        }

        // Check for X11 tools
        if (await _processRunner.CheckCommandAsync("xclip", cancellationToken))
        {
            _tool = ClipboardTool.Xclip;
            Log.Information("[LinuxClipboard] Using xclip");
            _initialized = true;
            return;
        }

        if (await _processRunner.CheckCommandAsync("xsel", cancellationToken))
        {
            _tool = ClipboardTool.Xsel;
            Log.Information("[LinuxClipboard] Using xsel");
            _initialized = true;
            return;
        }

        Log.Warning("[LinuxClipboard] No supported clipboard tool found (wl-copy/wl-paste, xclip, xsel missing)");
        _initialized = true;
    }

    public async Task SetTextAsync(string text, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        try
        {
            switch (_tool)
            {
                case ClipboardTool.WlClipboard:
                    await _processRunner.WriteInputAndCloseAsync("wl-copy", "--type text/plain", text, cancellationToken);
                    break;
                case ClipboardTool.Xclip:
                    await _processRunner.WriteInputAndCloseAsync("xclip", "-selection clipboard", text, cancellationToken);
                    break;
                case ClipboardTool.Xsel:
                    await _processRunner.WriteInputAndCloseAsync("xsel", "--clipboard --input", text, cancellationToken);
                    break;
                default:
                    Log.Warning("Cannot set clipboard: No tool available");
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to set clipboard text via shell");
        }
    }

    public async Task<string?> GetTextAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        try
        {
            return _tool switch
            {
                ClipboardTool.WlClipboard => await _processRunner.ReadCommandAsync("wl-paste", "--no-newline", cancellationToken),
                ClipboardTool.Xclip => await _processRunner.ReadCommandAsync("xclip", "-selection clipboard -o", cancellationToken),
                ClipboardTool.Xsel => await _processRunner.ReadCommandAsync("xsel", "--clipboard --output", cancellationToken),
                _ => null
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (_tool == ClipboardTool.WlClipboard && IsEmptyWlPasteResult(ex))
            {
                Log.Debug("[LinuxClipboard] Wayland clipboard is empty");
                return string.Empty;
            }

            Log.Error(ex, "Failed to get clipboard text via shell");
            return null;
        }
    }

    private static bool IsEmptyWlPasteResult(Exception ex)
    {
        return ex is InvalidOperationException &&
               ex.Message.Contains("Nothing is copied", StringComparison.Ordinal);
    }

}
