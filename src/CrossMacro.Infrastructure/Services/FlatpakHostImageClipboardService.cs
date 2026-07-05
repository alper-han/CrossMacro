using CrossMacro.Core.Logging;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Infrastructure.Services;

public sealed class FlatpakHostImageClipboardService : IImageClipboardService
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
        HostXclip
    }

    public FlatpakHostImageClipboardService(IProcessRunner processRunner, IRuntimeContext runtimeContext)
        : this(processRunner, runtimeContext, Environment.GetEnvironmentVariable)
    {
    }

    internal FlatpakHostImageClipboardService(
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

        if (!await _processRunner.CheckCommandAsync(FlatpakSpawn, cancellationToken).ConfigureAwait(false))
        {
            Log.Warning("[FlatpakHostImageClipboard] flatpak-spawn is not available in sandbox");
            _initialized = true;
            return;
        }

        if (IsWaylandSession() && await HostCommandExistsAsync("wl-copy", cancellationToken).ConfigureAwait(false))
        {
            _tool = ClipboardTool.HostWlClipboard;
            Log.Information("[FlatpakHostImageClipboard] Using host wl-copy via flatpak-spawn");
            _initialized = true;
            return;
        }

        if (IsX11CompatibleSession() && await HostCommandExistsAsync("xclip", cancellationToken).ConfigureAwait(false))
        {
            _tool = ClipboardTool.HostXclip;
            Log.Information("[FlatpakHostImageClipboard] Using host xclip via flatpak-spawn");
            _initialized = true;
            return;
        }

        Log.Warning("[FlatpakHostImageClipboard] No supported host image clipboard tool found (wl-copy or xclip missing)");
        _initialized = true;
    }

    public async Task SetPngAsync(ReadOnlyMemory<byte> pngBytes, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            switch (_tool)
            {
                case ClipboardTool.HostWlClipboard:
                    await _processRunner.WriteClipboardInputAndCloseAsync(
                        FlatpakSpawn,
                        ["--host", "wl-copy", "--type", "image/png"],
                        pngBytes,
                        cancellationToken).ConfigureAwait(false);
                    return;
                case ClipboardTool.HostXclip:
                    await _processRunner.WriteClipboardInputAndCloseAsync(
                        FlatpakSpawn,
                        ["--host", "xclip", "-selection", "clipboard", "-t", "image/png", "-i"],
                        pngBytes,
                        cancellationToken).ConfigureAwait(false);
                    return;
                default:
                    throw new ImageClipboardUnavailableException("No supported host image clipboard tool is available.");
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[FlatpakHostImageClipboard] Failed to set host image clipboard");
            throw;
        }
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
                cancellationToken).ConfigureAwait(false);
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
