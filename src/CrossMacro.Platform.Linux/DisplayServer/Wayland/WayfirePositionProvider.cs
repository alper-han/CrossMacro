
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

/// <summary>
/// Mouse position provider for Wayfire compositor.
/// Requires ipc and ipc-rules plugins to expose cursor/output methods.
/// </summary>
public sealed class WayfirePositionProvider : IMousePositionProvider, IAsyncDisposable
{
    private const string CursorMethod = "window-rules/get_cursor_position";
    private const string ListOutputsMethod = "window-rules/list-outputs";
    private static readonly TimeSpan CapabilityProbeTimeout = TimeSpan.FromSeconds(1);

    private readonly IWayfireIpcClient _ipcClient;
    private readonly SemaphoreSlim _layoutGate = new(1, 1);
    private readonly CancellationTokenSource _probeCts = new();
    private readonly Task _probeTask;

    private bool _disposed;
    private volatile bool _isSupported;
    private bool _hasLayout;

    public string ProviderName => "Wayfire IPC";
    public bool IsSupported => !_disposed && _isSupported;

    public WayfirePositionProvider() : this(new WayfireIpcClient()) { /* Empty */ }

    internal WayfirePositionProvider(IWayfireIpcClient ipcClient)
    {
        _ipcClient = ipcClient ?? throw new ArgumentNullException(nameof(ipcClient));
        _probeTask = ProbeCapabilitiesAsync(_probeCts.Token);
    }

    public async Task<(int X, int Y)?> GetAbsolutePositionAsync()
    {
        if (_disposed)
        {
            return null;
        }

        await _probeTask.ConfigureAwait(false);
        if (_disposed || !_isSupported)
        {
            return null;
        }

        await EnsureLayoutAsync(_probeCts.Token).ConfigureAwait(false);
        if (!_isSupported)
        {
            return null;
        }

        var response = await _ipcClient.SendRequestAsync(CursorMethod, _probeCts.Token).ConfigureAwait(false);
        if (!TryParseCursorPosition(response, out var rawX, out var rawY, out var methodUnavailable))
        {
            if (methodUnavailable)
            {
                DisableProvider("cursor method unavailable");
            }

            return null;
        }

        return (rawX, rawY);
    }

    public async Task<(int Width, int Height)?> GetScreenResolutionAsync()
    {
        var bounds = await GetDesktopBoundsAsync().ConfigureAwait(false);
        return bounds is not null ? (bounds.Value.Width, bounds.Value.Height) : null;
    }

    public async Task<ScreenRect?> GetDesktopBoundsAsync()
    {
        if (_disposed)
        {
            return null;
        }

        await _probeTask.ConfigureAwait(false);
        if (_disposed || !_isSupported)
        {
            return null;
        }

        var layout = await RefreshLayoutAsync(_probeCts.Token).ConfigureAwait(false);
        return layout is not null
            ? new ScreenRect(
                layout.Value.OriginX,
                layout.Value.OriginY,
                layout.Value.Width,
                layout.Value.Height)
            : null;
    }

    private async Task ProbeCapabilitiesAsync(CancellationToken cancellationToken)
    {
        if (!_ipcClient.IsAvailable)
        {
            return;
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(CapabilityProbeTimeout);

            var cursorResponse = await _ipcClient.SendRequestAsync(CursorMethod, cts.Token).ConfigureAwait(false);
            if (!TryParseCursorPosition(cursorResponse, out _, out _, out _))
            {
                _isSupported = false;
                Log.Debug("[WayfirePositionProvider] Capability probe failed; provider unavailable");
                return;
            }

            var outputsResponse = await _ipcClient.SendRequestAsync(ListOutputsMethod, cts.Token).ConfigureAwait(false);
            if (!TryParseOutputLayout(outputsResponse, out _, out _))
            {
                _isSupported = false;
                Log.Debug("[WayfirePositionProvider] Capability probe failed; provider unavailable");
                return;
            }

            MarkLayoutAvailable();
            _isSupported = true;
            Log.Information("[WayfirePositionProvider] Capability probe succeeded");
        }
        catch (OperationCanceledException)
        {
            if (!_disposed)
            {
                _isSupported = false;
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Debug(ex, "[WayfirePositionProvider] Capability probe error");
            _isSupported = false;
        }
    }

    private async Task EnsureLayoutAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _hasLayout))
        {
            return;
        }

        await _layoutGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_disposed || !_isSupported || Volatile.Read(ref _hasLayout))
            {
                return;
            }

            _ = await RefreshLayoutCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            _ = _layoutGate.Release();
        }
    }

    private async Task<OutputLayout?> RefreshLayoutAsync(CancellationToken cancellationToken)
    {
        await _layoutGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_disposed || !_isSupported)
            {
                return null;
            }

            return await RefreshLayoutCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            _ = _layoutGate.Release();
        }
    }

    private async Task<OutputLayout?> RefreshLayoutCoreAsync()
    {
        var response = await _ipcClient.SendRequestAsync(ListOutputsMethod, _probeCts.Token).ConfigureAwait(false);
        if (!TryParseOutputLayout(response, out var layout, out var methodUnavailable))
        {
            if (methodUnavailable)
            {
                DisableProvider("output listing method unavailable");
            }

            return null;
        }

        MarkLayoutAvailable();
        return layout;
    }

    private void MarkLayoutAvailable()
    {
        Volatile.Write(ref _hasLayout, true);
    }

    private void DisableProvider(string reason)
    {
        _isSupported = false;
        Log.Warning("[WayfirePositionProvider] Disabled provider: {Reason}", reason);
    }

    internal static bool TryParseCursorPosition(
        string? response,
        out int x,
        out int y,
        out bool methodUnavailable)
    {
        x = 0;
        y = 0;
        methodUnavailable = false;

        if (string.IsNullOrWhiteSpace(response))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            if (TryGetMethodError(root, out methodUnavailable))
            {
                return false;
            }

            if (root.ValueKind is not JsonValueKind.Object ||
                !root.TryGetProperty("pos", out var posElement) ||
                posElement.ValueKind is not JsonValueKind.Object)
            {
                return false;
            }

            if (!TryGetNumericValue(posElement, "x", out var xValue) ||
                !TryGetNumericValue(posElement, "y", out var yValue))
            {
                return false;
            }

            return TryRoundToInt32(xValue, out x) && TryRoundToInt32(yValue, out y);
        }
        catch (Exception ex) when (ex is JsonException or OverflowException)
        {
            return false;
        }
    }

    internal static bool TryParseOutputLayout(
        string? response,
        out OutputLayout layout,
        out bool methodUnavailable)
    {
        layout = default;
        methodUnavailable = false;

        if (string.IsNullOrWhiteSpace(response))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            if (TryGetMethodError(root, out methodUnavailable))
            {
                return false;
            }

            if (!TryGetOutputArray(root, out var outputs))
            {
                return false;
            }

            return TryCalculateOutputLayout(outputs, out layout);
        }
        catch (Exception ex) when (ex is JsonException or OverflowException)
        {
            return false;
        }
    }

    private static bool TryGetOutputArray(JsonElement root, out JsonElement outputs)
    {
        if (root.ValueKind is JsonValueKind.Array)
        {
            outputs = root;
            return true;
        }

        if (root.ValueKind is JsonValueKind.Object &&
            root.TryGetProperty("outputs", out var outputsElement) &&
            outputsElement.ValueKind is JsonValueKind.Array)
        {
            outputs = outputsElement;
            return true;
        }

        outputs = default;
        return false;
    }

    private static bool TryCalculateOutputLayout(JsonElement outputs, out OutputLayout layout)
    {
        layout = default;

        bool hasAnyGeometry = false;
        int minX = 0;
        int minY = 0;
        int maxX = 0;
        int maxY = 0;

        foreach (var output in outputs.EnumerateArray())
        {
            if (!TryGetOutputBounds(output, out var x, out var y, out var right, out var bottom))
            {
                continue;
            }

            if (!hasAnyGeometry)
            {
                hasAnyGeometry = true;
                minX = x;
                minY = y;
                maxX = right;
                maxY = bottom;
                continue;
            }

            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, right);
            maxY = Math.Max(maxY, bottom);
        }

        if (!hasAnyGeometry)
        {
            return false;
        }

        layout = new OutputLayout(
            OriginX: minX,
            OriginY: minY,
            Width: checked(maxX - minX),
            Height: checked(maxY - minY));

        return layout.Width > 0 && layout.Height > 0;
    }

    private static bool TryGetOutputBounds(JsonElement output, out int x, out int y, out int right, out int bottom)
    {
        x = 0;
        y = 0;
        right = 0;
        bottom = 0;

        if (output.ValueKind is not JsonValueKind.Object ||
            !output.TryGetProperty("geometry", out var geometry) ||
            geometry.ValueKind is not JsonValueKind.Object ||
            !TryGetNumericValue(geometry, "x", out var gx) ||
            !TryGetNumericValue(geometry, "y", out var gy) ||
            !TryGetNumericValue(geometry, "width", out var gw) ||
            !TryGetNumericValue(geometry, "height", out var gh))
        {
            return false;
        }

        if (!TryRoundToInt32(gx, out x) ||
            !TryRoundToInt32(gy, out y) ||
            !TryRoundToInt32(gw, out int width) ||
            !TryRoundToInt32(gh, out int height) ||
            width <= 0 ||
            height <= 0)
        {
            return false;
        }

        right = checked(x + width);
        bottom = checked(y + height);
        return true;
    }

    private static bool TryRoundToInt32(double value, out int result)
    {
        result = 0;
        if (!double.IsFinite(value))
        {
            return false;
        }

        double rounded = Math.Round(value, MidpointRounding.AwayFromZero);
        if (rounded is < int.MinValue or > int.MaxValue)
        {
            return false;
        }

        result = (int)rounded;
        return true;
    }

    private static bool TryGetMethodError(JsonElement root, out bool methodUnavailable)
    {
        methodUnavailable = false;

        if (root.ValueKind is not JsonValueKind.Object || !root.TryGetProperty("error", out var errorElement))
        {
            return false;
        }

        if (errorElement.ValueKind is not JsonValueKind.String)
        {
            return true;
        }

        var errorText = errorElement.GetString();
        methodUnavailable = !string.IsNullOrWhiteSpace(errorText) &&
            errorText.Contains("No such method", StringComparison.OrdinalIgnoreCase);
        return true;
    }

    private static bool TryGetNumericValue(JsonElement element, string property, out double value)
    {
        value = 0;

        if (!element.TryGetProperty(property, out var prop))
        {
            return false;
        }

        if (prop.ValueKind is JsonValueKind.Number)
        {
            return prop.TryGetDouble(out value);
        }

        return false;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _probeCts.CancelAsync().ConfigureAwait(false);
        await _probeTask.ConfigureAwait(false);
        _probeCts.Dispose();
        _layoutGate.Dispose();
        _ipcClient.Dispose();
        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
