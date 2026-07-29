
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

/// <summary>
/// Niri provider for display geometry. Cursor position is intentionally unsupported
/// until Niri exposes a safe absolute cursor API suitable for recording.
/// </summary>
public sealed class NiriPositionProvider : IMousePositionProvider
{
    private const string OutputsRequestJson = "\"Outputs\"";

    private readonly INiriIpcClient _ipcClient;
    private bool _disposed;

    public NiriPositionProvider()
        : this(new NiriIpcClient()) { /* Empty */ }

    internal NiriPositionProvider(INiriIpcClient ipcClient)
    {
        _ipcClient = ipcClient ?? throw new ArgumentNullException(nameof(ipcClient));
    }

    public string ProviderName => "Niri IPC (Resolution Only)";

    public bool IsSupported => false;

    public Task<(int X, int Y)?> GetAbsolutePositionAsync()
    {
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
            var response = await _ipcClient.SendRequestAsync(OutputsRequestJson).ConfigureAwait(false);
            if (TryParseScreenResolution(response, out var width, out var height))
            {
                Log.Information("[NiriPositionProvider] Screen resolution detected: {Width}x{Height}", width, height);
                return (width, height);
            }

            Log.Warning("[NiriPositionProvider] Failed to parse screen resolution from Niri outputs response");
            return null;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "[NiriPositionProvider] Failed to get screen resolution");
            return null;
        }
    }

    internal static bool TryParseScreenResolution(string? response, out int width, out int height)
    {
        width = 0;
        height = 0;

        if (string.IsNullOrWhiteSpace(response))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(response);
            var root = document.RootElement;

            if (!TryGetOutputsElement(root, out var outputsElement) || outputsElement.ValueKind is not JsonValueKind.Object)
            {
                return false;
            }

            var bounds = outputsElement.EnumerateObject()
                .Select(TryGetOutputBounds)
                .Where(static b => b is not null)
                .Cast<(int X, int Y, int Right, int Bottom)>()
                .ToList();

            if (bounds.Count is 0)
            {
                return false;
            }

            return TryComputeScreenExtents(bounds, out width, out height);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static (int X, int Y, int Right, int Bottom)? TryGetOutputBounds(JsonProperty outputProperty)
    {
        var output = outputProperty.Value;
        if (output.ValueKind is not JsonValueKind.Object || !IsOutputEnabled(output))
        {
            return null;
        }

        if (!output.TryGetProperty("logical", out var logical) || logical.ValueKind is not JsonValueKind.Object)
        {
            return null;
        }

        if (!TryGetInt32(logical, "x", out var x) ||
            !TryGetInt32(logical, "y", out var y) ||
            !TryGetPositiveInt32(logical, "width", out var logicalWidth) ||
            !TryGetPositiveInt32(logical, "height", out var logicalHeight))
        {
            return null;
        }

        return (x, y, x + logicalWidth, y + logicalHeight);
    }

    private static bool TryComputeScreenExtents(List<(int X, int Y, int Right, int Bottom)> bounds, out int width, out int height)
    {
        width = 0;
        height = 0;

        var minX = bounds.Min(static b => b.X);
        var minY = bounds.Min(static b => b.Y);
        var maxX = bounds.Max(static b => b.Right);
        var maxY = bounds.Max(static b => b.Bottom);

        if (maxX <= minX || maxY <= minY)
        {
            return false;
        }

        width = maxX - minX;
        height = maxY - minY;
        return true;
    }

    private static bool TryGetOutputsElement(JsonElement root, out JsonElement outputsElement)
    {
        outputsElement = default;

        if (root.ValueKind is not JsonValueKind.Object)
        {
            return false;
        }

        if (root.TryGetProperty("Outputs", out outputsElement))
        {
            return true;
        }

        if (root.TryGetProperty("Ok", out var okElement))
        {
            if (okElement.ValueKind is JsonValueKind.Object && okElement.TryGetProperty("Outputs", out outputsElement))
            {
                return true;
            }

            if (okElement.ValueKind is JsonValueKind.Object)
            {
                outputsElement = okElement;
                return true;
            }
        }

        outputsElement = root;
        return true;
    }

    private static bool IsOutputEnabled(JsonElement output)
    {
        return output.TryGetProperty("current_mode", out var currentMode) &&
               currentMode.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined);
    }

    private static bool TryGetPositiveInt32(JsonElement element, string propertyName, out int value)
    {
        return TryGetInt32(element, propertyName, out value) && value > 0;
    }

    private static bool TryGetInt32(JsonElement element, string propertyName, out int value)
    {
        value = 0;
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        return property.ValueKind is JsonValueKind.Number && property.TryGetInt32(out value);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _ipcClient.Dispose();
        GC.SuppressFinalize(this);
    }
}
