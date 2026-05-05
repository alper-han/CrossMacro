using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Logging;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Clipboard service for Flatpak sandboxes that delegates clipboard access to the host session.
/// </summary>
public sealed class FlatpakHostClipboardService : IClipboardService
{
    private const string FlatpakSpawn = "flatpak-spawn";
    private readonly IProcessRunner _processRunner;
    private readonly IRuntimeContext _runtimeContext;
    private readonly Func<string, string?> _getEnvironmentVariable;
    private ClipboardTool _tool = ClipboardTool.Unknown;
    private bool _initialized;

    private enum ClipboardTool
    {
        Unknown,
        HostWlClipboard,
        HostXclip,
        HostXsel
    }

    public FlatpakHostClipboardService(IProcessRunner processRunner, IRuntimeContext runtimeContext)
        : this(processRunner, runtimeContext, Environment.GetEnvironmentVariable)
    {
    }

    internal FlatpakHostClipboardService(
        IProcessRunner processRunner,
        IRuntimeContext runtimeContext,
        Func<string, string?> getEnvironmentVariable)
    {
        _processRunner = processRunner;
        _runtimeContext = runtimeContext;
        _getEnvironmentVariable = getEnvironmentVariable;
    }

    public bool IsSupported => _tool != ClipboardTool.Unknown || !_initialized;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        if (!await _processRunner.CheckCommandAsync(FlatpakSpawn, cancellationToken))
        {
            Log.Warning("[FlatpakHostClipboard] flatpak-spawn is not available in sandbox");
            _initialized = true;
            return;
        }

        if (IsWaylandSession() &&
            await HostCommandExistsAsync("wl-copy", cancellationToken) &&
            await HostCommandExistsAsync("wl-paste", cancellationToken))
        {
            _tool = ClipboardTool.HostWlClipboard;
            Log.Information("[FlatpakHostClipboard] Using host wl-clipboard via flatpak-spawn");
            _initialized = true;
            return;
        }

        if (IsX11CompatibleSession() && await HostCommandExistsAsync("xclip", cancellationToken))
        {
            _tool = ClipboardTool.HostXclip;
            Log.Information("[FlatpakHostClipboard] Using host xclip via flatpak-spawn");
            _initialized = true;
            return;
        }

        if (IsX11CompatibleSession() && await HostCommandExistsAsync("xsel", cancellationToken))
        {
            _tool = ClipboardTool.HostXsel;
            Log.Information("[FlatpakHostClipboard] Using host xsel via flatpak-spawn");
            _initialized = true;
            return;
        }

        Log.Warning("[FlatpakHostClipboard] No supported host clipboard tool found (wl-copy/wl-paste, xclip, xsel missing)");
        _initialized = true;
    }

    public async Task SetTextAsync(string text, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        try
        {
            switch (_tool)
            {
                case ClipboardTool.HostWlClipboard:
                    await _processRunner.WriteInputAndCloseAsync(
                        FlatpakSpawn,
                        ["--host", "wl-copy", "--type", "text/plain"],
                        text,
                        cancellationToken);
                    return;
                case ClipboardTool.HostXclip:
                    await _processRunner.WriteInputAndCloseAsync(
                        FlatpakSpawn,
                        ["--host", "xclip", "-selection", "clipboard"],
                        text,
                        cancellationToken);
                    return;
                case ClipboardTool.HostXsel:
                    await _processRunner.WriteInputAndCloseAsync(
                        FlatpakSpawn,
                        ["--host", "xsel", "--clipboard", "--input"],
                        text,
                        cancellationToken);
                    return;
                default:
                    Log.Warning("[FlatpakHostClipboard] Cannot set clipboard: no host clipboard tool available");
                    return;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[FlatpakHostClipboard] Failed to set host clipboard text");
            throw;
        }
    }

    public async Task<string?> GetTextAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        try
        {
            return _tool switch
            {
                ClipboardTool.HostWlClipboard => await _processRunner.ReadCommandAsync(
                    FlatpakSpawn,
                    ["--host", "wl-paste", "--no-newline"],
                    cancellationToken),
                ClipboardTool.HostXclip => await _processRunner.ReadCommandAsync(
                    FlatpakSpawn,
                    ["--host", "xclip", "-selection", "clipboard", "-o"],
                    cancellationToken),
                ClipboardTool.HostXsel => await _processRunner.ReadCommandAsync(
                    FlatpakSpawn,
                    ["--host", "xsel", "--clipboard", "--output"],
                    cancellationToken),
                _ => null
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (_tool == ClipboardTool.HostWlClipboard && IsEmptyWlPasteResult(ex))
            {
                Log.Debug("[FlatpakHostClipboard] Host Wayland clipboard is empty");
                return string.Empty;
            }

            Log.Error(ex, "[FlatpakHostClipboard] Failed to get host clipboard text");
            throw;
        }
    }

    private static bool IsEmptyWlPasteResult(Exception ex)
    {
        return ex is InvalidOperationException &&
               ex.Message.Contains("Nothing is copied", StringComparison.Ordinal);
    }

    private bool IsWaylandSession()
    {
        if (string.Equals(_runtimeContext.SessionType, "wayland", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(_runtimeContext.SessionType, "x11", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(_getEnvironmentVariable("WAYLAND_DISPLAY"));
    }

    private bool IsX11CompatibleSession()
    {
        return string.Equals(_runtimeContext.SessionType, "x11", StringComparison.OrdinalIgnoreCase) ||
               (!IsWaylandSession() && !string.IsNullOrWhiteSpace(_getEnvironmentVariable("DISPLAY")));
    }

    private async Task<bool> HostCommandExistsAsync(string command, CancellationToken cancellationToken)
    {
        try
        {
            var output = await _processRunner.ReadCommandAsync(
                FlatpakSpawn,
                ["--host", "sh", "-lc", $"command -v {command} >/dev/null 2>&1 && printf yes"],
                cancellationToken);
            return string.Equals(output.Trim(), "yes", StringComparison.Ordinal);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }
}
