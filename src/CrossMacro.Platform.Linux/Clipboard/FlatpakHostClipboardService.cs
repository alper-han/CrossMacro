
namespace CrossMacro.Platform.Linux.Clipboard;

/// <summary>
/// Clipboard service for Flatpak sandboxes that delegates clipboard access to the host session.
/// </summary>
public sealed class FlatpakHostClipboardService(
    IProcessRunner processRunner,
    IRuntimeContext runtimeContext,
    Func<string, string?> getEnvironmentVariable) : IHostClipboardService
{
    private const string FlatpakSpawn = "flatpak-spawn";
    private readonly IProcessRunner _processRunner = processRunner;
    private readonly IRuntimeContext _runtimeContext = runtimeContext;
    private readonly Func<string, string?> _getEnvironmentVariable = getEnvironmentVariable;
    private readonly LinuxEnvironmentSnapshot? _environment;
    private ClipboardTool _tool = ClipboardTool.Unknown;
    private bool _initialized;

    private enum ClipboardTool
    {
        Unknown,
        HostWlClipboard,
        HostXclip,
        HostXsel,
    }

    public FlatpakHostClipboardService(IProcessRunner processRunner, IRuntimeContext runtimeContext)
        : this(processRunner, runtimeContext, LinuxEnvironmentVariables.CaptureCurrentSnapshot()) { /* Empty */ }

    public FlatpakHostClipboardService(
        IProcessRunner processRunner,
        IRuntimeContext runtimeContext,
        LinuxEnvironmentSnapshot environment)
        : this(processRunner, runtimeContext, static _ => null)
    {
        _environment = environment;
    }

    public bool IsSupported => _tool is not ClipboardTool.Unknown || !_initialized;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        if (!await _processRunner.CheckCommandAsync(FlatpakSpawn, cancellationToken).ConfigureAwait(false))
        {
            Log.Warning("[FlatpakHostClipboard] flatpak-spawn is not available in sandbox");
            _initialized = true;
            return;
        }

        if (IsWaylandSession() &&
            await HostCommandExistsAsync("wl-copy", cancellationToken).ConfigureAwait(false) &&
            await HostCommandExistsAsync("wl-paste", cancellationToken).ConfigureAwait(false))
        {
            _tool = ClipboardTool.HostWlClipboard;
            Log.Information("[FlatpakHostClipboard] Using host wl-clipboard via flatpak-spawn");
            _initialized = true;
            return;
        }

        if (IsX11CompatibleSession() && await HostCommandExistsAsync("xclip", cancellationToken).ConfigureAwait(false))
        {
            _tool = ClipboardTool.HostXclip;
            Log.Information("[FlatpakHostClipboard] Using host xclip via flatpak-spawn");
            _initialized = true;
            return;
        }

        if (IsX11CompatibleSession() && await HostCommandExistsAsync("xsel", cancellationToken).ConfigureAwait(false))
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
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            switch (_tool)
            {
                case ClipboardTool.HostWlClipboard:
                    await _processRunner.WriteClipboardInputAndCloseAsync(
                        FlatpakSpawn,
                        ["--host", "wl-copy", "--type", "text/plain"],
                        text,
                        cancellationToken).ConfigureAwait(false);
                    return;
                case ClipboardTool.HostXclip:
                    await _processRunner.WriteClipboardInputAndCloseAsync(
                        FlatpakSpawn,
                        ["--host", "xclip", "-selection", "clipboard"],
                        text,
                        cancellationToken).ConfigureAwait(false);
                    return;
                case ClipboardTool.HostXsel:
                    await _processRunner.WriteClipboardInputAndCloseAsync(
                        FlatpakSpawn,
                        ["--host", "xsel", "--clipboard", "--input"],
                        text,
                        cancellationToken).ConfigureAwait(false);
                    return;
                default:
                    throw new InvalidOperationException("No supported host clipboard tool is available.");
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "[FlatpakHostClipboard] Failed to set host clipboard text");
            throw;
        }
    }

    public async Task<string?> GetTextAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return _tool switch
            {
                ClipboardTool.HostWlClipboard => await _processRunner.ReadCommandAsync(
                    FlatpakSpawn,
                    ["--host", "wl-paste", "--no-newline"],
                    cancellationToken).ConfigureAwait(false),
                ClipboardTool.HostXclip => await _processRunner.ReadCommandAsync(
                    FlatpakSpawn,
                    ["--host", "xclip", "-selection", "clipboard", "-o"],
                    cancellationToken).ConfigureAwait(false),
                ClipboardTool.HostXsel => await _processRunner.ReadCommandAsync(
                    FlatpakSpawn,
                    ["--host", "xsel", "--clipboard", "--output"],
                    cancellationToken).ConfigureAwait(false),
                ClipboardTool.Unknown => throw new InvalidOperationException("No supported host clipboard tool is available."),
                _ => throw new SwitchExpressionException(_tool),
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (_tool is ClipboardTool.HostWlClipboard && IsEmptyWlPasteResult(ex))
            {
                Log.Debug("[FlatpakHostClipboard] Host Wayland clipboard is empty");
                return string.Empty;
            }

            Log.LogError(ex, "[FlatpakHostClipboard] Failed to get host clipboard text");
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

        return !string.IsNullOrWhiteSpace(_environment?.WaylandDisplay ?? _getEnvironmentVariable("WAYLAND_DISPLAY"));
    }

    private bool IsX11CompatibleSession()
    {
        return string.Equals(_runtimeContext.SessionType, "x11", StringComparison.OrdinalIgnoreCase) ||
               (!IsWaylandSession() && !string.IsNullOrWhiteSpace(_environment?.Display ?? _getEnvironmentVariable("DISPLAY")));
    }

    private async Task<bool> HostCommandExistsAsync(string command, CancellationToken cancellationToken)
    {
        try
        {
            var output = await _processRunner.ReadCommandAsync(
                FlatpakSpawn,
                ["--host", "sh", "-lc", $"command -v {command} >/dev/null 2>&1 && printf yes"],
                cancellationToken).ConfigureAwait(false);
            return string.Equals(output.Trim(), "yes", StringComparison.Ordinal);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return false;
        }
    }
}
