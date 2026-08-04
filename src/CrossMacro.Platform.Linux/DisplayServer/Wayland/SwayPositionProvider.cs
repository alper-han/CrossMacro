
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
        : this(new SwayIpcClient()) { /* Empty */ }

    internal SwayPositionProvider(ISwayIpcClient ipcClient)
    {
        _ipcClient = ipcClient ?? throw new ArgumentNullException(nameof(ipcClient));
    }

    public string ProviderName => "Sway IPC (Resolution Only)";

    public bool IsSupported => _ipcClient.IsAvailable;

    public bool SupportsAbsolutePosition => false;

    public Task<(int X, int Y)?> GetAbsolutePositionAsync()
    {
        // Sway natively blocks global absolute pointer polling via IPC/Wayland.
        return Task.FromResult<(int X, int Y)?>(null);
    }

    public async Task<(int Width, int Height)?> GetScreenResolutionAsync()
    {
        var bounds = await GetDesktopBoundsAsync().ConfigureAwait(false);
        return bounds is not null ? (bounds.Value.Width, bounds.Value.Height) : null;
    }

    public async Task<ScreenRect?> GetDesktopBoundsAsync()
    {
        if (_disposed || !_ipcClient.IsAvailable)
        {
            return null;
        }

        try
        {
            var response = await _ipcClient.SendRequestAsync(IpcGetOutputs).ConfigureAwait(false);
            if (string.IsNullOrEmpty(response))
            {
                return null;
            }

            if (!TryParseDesktopBounds(response, out var bounds))
            {
                return null;
            }

            Log.Information(
                "[SwayPositionProvider] Desktop bounds detected: ({X},{Y}) {Width}x{Height}",
                bounds.X,
                bounds.Y,
                bounds.Width,
                bounds.Height);
            return bounds;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "[SwayPositionProvider] Failed to get screen resolution");
            return null;
        }
    }

    internal static bool TryParseDesktopBounds(string response, out ScreenRect bounds)
    {
        bounds = default;
        try
        {
            var outputs = JsonSerializer.Deserialize(response, SwayJsonContext.Default.SwayOutputDtoArray);
            if (outputs is null || outputs.Length is 0)
            {
                return false;
            }

            var activeOutputs = outputs
                .Where(static output => output.Active && output.Rect is { Width: > 0, Height: > 0 })
                .ToArray();
            if (activeOutputs.Length is 0)
            {
                return false;
            }

            int minX = activeOutputs.Min(static output => output.Rect!.X);
            int minY = activeOutputs.Min(static output => output.Rect!.Y);
            int maxX = activeOutputs.Max(static output => checked(output.Rect!.X + output.Rect.Width));
            int maxY = activeOutputs.Max(static output => checked(output.Rect!.Y + output.Rect.Height));
            if (maxX <= minX || maxY <= minY)
            {
                return false;
            }

            bounds = new ScreenRect(minX, minY, checked(maxX - minX), checked(maxY - minY));
            return true;
        }
        catch (Exception ex) when (ex is JsonException or OverflowException)
        {
            return false;
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
