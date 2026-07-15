
namespace CrossMacro.Platform.Linux.Clipboard;

/// <summary>
/// Clipboard service that uses Linux command line tools (wl-copy, xclip)
/// to ensure reliable background operation where GUI frameworks fail.
/// </summary>
public class LinuxShellClipboardService : ILinuxClipboardService
{
    private readonly IProcessRunner _processRunner;
    private enum ClipboardTool { Unknown, WlClipboard, Xclip, Xsel }
    private ClipboardTool _tool = ClipboardTool.Unknown;
    private bool _initialized = false;
    private readonly LinuxEnvironmentSnapshot _environment;

    public bool IsSupported => _tool is not ClipboardTool.Unknown || !_initialized;

    public LinuxShellClipboardService(IProcessRunner processRunner)
        : this(processRunner, LinuxEnvironmentVariables.CaptureCurrentSnapshot())
    {
    }

    public LinuxShellClipboardService(IProcessRunner processRunner, LinuxEnvironmentSnapshot environment)
    {
        _processRunner = processRunner;
        _environment = environment;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        // Check for Wayland first
        if (!string.IsNullOrEmpty(_environment.WaylandDisplay) && await _processRunner.CheckCommandAsync("wl-copy", cancellationToken).ConfigureAwait(false) &&
                await _processRunner.CheckCommandAsync("wl-paste", cancellationToken).ConfigureAwait(false))
        {
            _tool = ClipboardTool.WlClipboard;
            Log.Information("[LinuxClipboard] Detected Wayland, using wl-clipboard");
            _initialized = true;
            return;
        }

        // Check for X11 tools
        if (await _processRunner.CheckCommandAsync("xclip", cancellationToken).ConfigureAwait(false))
        {
            _tool = ClipboardTool.Xclip;
            Log.Information("[LinuxClipboard] Using xclip");
            _initialized = true;
            return;
        }

        if (await _processRunner.CheckCommandAsync("xsel", cancellationToken).ConfigureAwait(false))
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
        ArgumentNullException.ThrowIfNull(text);
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            switch (_tool)
            {
                case ClipboardTool.WlClipboard:
                    if (text.Length is 0)
                    {
                        await _processRunner.ExecuteCommandAsync("wl-copy", ["--clear"], cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await _processRunner.WriteClipboardInputAndCloseAsync("wl-copy", "--type text/plain", text, cancellationToken).ConfigureAwait(false);
                    }
                    break;
                case ClipboardTool.Xclip:
                    await _processRunner.WriteClipboardInputAndCloseAsync("xclip", "-selection clipboard", text, cancellationToken).ConfigureAwait(false);
                    break;
                case ClipboardTool.Xsel:
                    await _processRunner.WriteClipboardInputAndCloseAsync("xsel", "--clipboard --input", text, cancellationToken).ConfigureAwait(false);
                    break;
                default:
                    throw new InvalidOperationException("No supported Linux clipboard tool is available.");
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.LogError(ex, "Failed to set clipboard text via shell");
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
                ClipboardTool.WlClipboard => await _processRunner.ReadCommandAsync("wl-paste", "--no-newline", cancellationToken).ConfigureAwait(false),
                ClipboardTool.Xclip => await _processRunner.ReadCommandAsync("xclip", "-selection clipboard -o", cancellationToken).ConfigureAwait(false),
                ClipboardTool.Xsel => await _processRunner.ReadCommandAsync("xsel", "--clipboard --output", cancellationToken).ConfigureAwait(false),
                _ => throw new InvalidOperationException("No supported Linux clipboard tool is available."),
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (_tool is ClipboardTool.WlClipboard && IsEmptyWlPasteResult(ex))
            {
                Log.Debug("[LinuxClipboard] Wayland clipboard is empty");
                return string.Empty;
            }

            Log.LogError(ex, "Failed to get clipboard text via shell");
            throw;
        }
    }

    private static bool IsEmptyWlPasteResult(Exception ex)
    {
        return ex is InvalidOperationException &&
               ex.Message.Contains("Nothing is copied", StringComparison.Ordinal);
    }

}
