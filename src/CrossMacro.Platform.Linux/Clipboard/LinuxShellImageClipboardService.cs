using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Logging;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.Services;

namespace CrossMacro.Platform.Linux.Clipboard;

public sealed class LinuxShellImageClipboardService : IImageClipboardService
{
    private readonly IProcessRunner _processRunner;
    private readonly LinuxEnvironmentSnapshot _environment;
    private ClipboardTool _tool = ClipboardTool.Unknown;
    private bool _initialized;

    private enum ClipboardTool
    {
        Unknown,
        WlClipboard,
        Xclip,
    }

    public LinuxShellImageClipboardService(IProcessRunner processRunner)
        : this(processRunner, LinuxEnvironmentVariables.CaptureCurrentSnapshot())
    {
    }

    public LinuxShellImageClipboardService(IProcessRunner processRunner, LinuxEnvironmentSnapshot environment)
    {
        _processRunner = processRunner;
        _environment = environment;
    }

    public bool IsSupported => _tool is not ClipboardTool.Unknown || !_initialized;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        if (!string.IsNullOrEmpty(_environment.WaylandDisplay) &&
            await _processRunner.CheckCommandAsync("wl-copy", cancellationToken))
        {
            _tool = ClipboardTool.WlClipboard;
            Log.Information("[LinuxImageClipboard] Detected Wayland, using wl-copy");
            _initialized = true;
            return;
        }

        if (await _processRunner.CheckCommandAsync("xclip", cancellationToken))
        {
            _tool = ClipboardTool.Xclip;
            Log.Information("[LinuxImageClipboard] Using xclip");
            _initialized = true;
            return;
        }

        Log.Warning("[LinuxImageClipboard] No supported image clipboard tool found (wl-copy or xclip missing)");
        _initialized = true;
    }

    public async Task SetPngAsync(ReadOnlyMemory<byte> pngBytes, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            switch (_tool)
            {
                case ClipboardTool.WlClipboard:
                    await _processRunner.WriteClipboardInputAndCloseAsync(
                        "wl-copy",
                        ["--type", "image/png"],
                        pngBytes,
                        cancellationToken).ConfigureAwait(false);
                    return;
                case ClipboardTool.Xclip:
                    await _processRunner.WriteClipboardInputAndCloseAsync(
                        "xclip",
                        ["-selection", "clipboard", "-t", "image/png", "-i"],
                        pngBytes,
                        cancellationToken).ConfigureAwait(false);
                    return;
                default:
                    throw new ImageClipboardUnavailableException("No supported Linux image clipboard tool is available.");
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to set image clipboard via shell");
            throw;
        }
    }
}
