using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CrossMacro.Core.Logging;
using CrossMacro.Core.Services;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

/// <summary>
/// Sway provider for display geometry. Cursor position is intentionally unsupported
/// because Sway explicitly does not expose an absolute cursor-position API over IPC.
/// CrossMacro will fall back to relative mouse coordinates.
/// </summary>
public sealed class SwayPositionProvider : IMousePositionProvider
{
    private const uint IpcGetOutputs = 3;

    private readonly ISwayIpcClient _ipcClient;
    private bool _disposed;

    public SwayPositionProvider()
        : this(new SwayIpcClient())
    {
    }

    internal SwayPositionProvider(ISwayIpcClient ipcClient)
    {
        _ipcClient = ipcClient ?? throw new ArgumentNullException(nameof(ipcClient));
    }

    public string ProviderName => "Sway IPC (Resolution Only)";

    public bool IsSupported => _ipcClient.IsAvailable;

    public Task<(int X, int Y)?> GetAbsolutePositionAsync()
    {
        // Sway natively blocks global absolute pointer polling via IPC/Wayland.
        return Task.FromResult<(int X, int Y)?>(null);
    }

    public async Task<(int Width, int Height)?> GetScreenResolutionAsync()
    {
        if (_disposed || !_ipcClient.IsAvailable)
        {
            return null;
        }

        try
        {
            var response = await _ipcClient.SendRequestAsync(IpcGetOutputs).ConfigureAwait(false);
            if (string.IsNullOrEmpty(response))
                return null;

            var outputs = JsonSerializer.Deserialize(response, SwayJsonContext.Default.SwayOutputDtoArray);
            if (outputs == null || outputs.Length == 0)
                return null;

            var activeOutputs = outputs.Where(o => o.Active && o.Rect != null).ToArray();
            if (activeOutputs.Length == 0)
                return null;

            int minX = activeOutputs.Min(o => o.Rect!.X);
            int minY = activeOutputs.Min(o => o.Rect!.Y);
            int maxX = activeOutputs.Max(o => o.Rect!.X + o.Rect.Width);
            int maxY = activeOutputs.Max(o => o.Rect!.Y + o.Rect.Height);

            if (maxX <= minX || maxY <= minY)
                return null;

            int width = maxX - minX;
            int height = maxY - minY;

            Log.Information("[SwayPositionProvider] Screen resolution detected: {Width}x{Height}", width, height);
            return (width, height);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[SwayPositionProvider] Failed to get screen resolution");
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
